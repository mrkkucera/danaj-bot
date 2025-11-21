using Discord;
using Discord.WebSocket;

namespace DanajBot.Commands.Zkouska;

/// <summary>
/// Handles reaction-based interactions for zkouska messages
/// </summary>
internal class ZkouskaReactionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly ZkouskaState _state;
    private readonly ILogger<ZkouskaReactionHandler> _logger;

    public ZkouskaReactionHandler(
        DiscordSocketClient client,
        ZkouskaState state,
        ILogger<ZkouskaReactionHandler> logger)
    {
        _client = client;
        _state = state;
        _logger = logger;
    }

    /// <summary>
    /// Handles delete reaction from moderators
    /// </summary>
    public async Task<bool> HandleDeleteReactionAsync(IUserMessage message, IUser user, SocketGuildUser? member, ulong threadId)
    {
        if (!IsModeratorInChannel(member, message.Channel))
        {
            _logger.LogWarning("⚠️ {Username} attempted to delete zkouska but lacks moderator permissions", user.Username);
            await message.RemoveReactionAsync(new Emoji(ZkouskaConstants.DeleteEmoji), user);
            return false;
        }

        try
        {
            _logger.LogInformation("🗑️ {Username} is deleting zkouska message {MessageId}", user.Username, message.Id);

            var zkouskaId = ZkouskaHelper.ExtractZkouskaId(message.Content) ?? "Unknown";

            await CloseZkouskaThreadAsync(threadId, member!, user);
            await message.DeleteAsync();
            CleanupZkouskaState(message.Id);

            _logger.LogInformation("✅ {Username} successfully deleted zkouska {ZkouskaId}", user.Username, zkouskaId);
            return true;
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Error deleting zkouska message");
            return false;
        }
    }

    /// <summary>
    /// Handles absence reaction from users
    /// </summary>
    public async Task<bool> HandleAbsenceReactionAsync(IUserMessage message, IUser user, SocketGuildUser? member, ulong threadId)
    {
        return await HandleStatusReactionAsync(
            message,
            user,
            member,
            threadId,
            ZkouskaConstants.AbsenceEmoji,
            ZkouskaReactionType.Absence,
            ZkouskaMessageBuilder.CreateAbsenceEmbed);
    }

    /// <summary>
    /// Handles late arrival reaction from users
    /// </summary>
    public async Task<bool> HandleLateReactionAsync(IUserMessage message, IUser user, SocketGuildUser? member, ulong threadId)
    {
        return await HandleStatusReactionAsync(
            message,
            user,
            member,
            threadId,
            ZkouskaConstants.LateEmoji,
            ZkouskaReactionType.Late,
            ZkouskaMessageBuilder.CreateLateEmbed);
    }

    /// <summary>
    /// Handles attending reaction from users - clears any previous absence/late status
    /// </summary>
    public async Task<bool> HandleAttendingReactionAsync(IUserMessage message, IUser user, SocketGuildUser? member, ulong threadId)
    {
        var key = (message.Id, user.Id);
        var hasReacted = HasUserReacted(message.Id, user.Id, out var reactedUsers);

        try
        {
            var displayName = ZkouskaHelper.GetDisplayName(member?.DisplayName, user.Username);

            if (hasReacted && _state.UserReactionToThreadMessageId.TryGetValue(key, out var existingMessageId))
            {
                await DeleteUserReactionMessageAsync(threadId, existingMessageId);
                CleanupUserReaction(key, reactedUsers);
                
                _logger.LogInformation("🗑️ {DisplayName} ({Username}) potvrdil účast - odstraněna předchozí reakce", 
                    displayName, user.Username);
            }
            else
            {
                _logger.LogInformation("✅ {DisplayName} ({Username}) potvrdil účast (žádná předchozí reakce)", 
                    displayName, user.Username);
            }

            await message.RemoveReactionAsync(new Emoji(ZkouskaConstants.AttendingEmoji), user);
            _logger.LogInformation("🧹 Removed reaction from {DisplayName} to hide count", displayName);

            return true;
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Error processing attending reaction");
            return false;
        }
    }

    private async Task<bool> HandleStatusReactionAsync(
        IUserMessage message,
        IUser user,
        SocketGuildUser? member,
        ulong threadId,
        string emoji,
        ZkouskaReactionType reactionType,
        Func<string, string, ulong, Embed> createEmbed)
    {
        var key = (message.Id, user.Id);
        var hasReacted = HasUserReacted(message.Id, user.Id, out var reactedUsers);

        try
        {
            var thread = await GetThreadAsync(threadId);
            if (thread == null)
            {
                return false;
            }

            var displayName = ZkouskaHelper.GetDisplayName(member?.DisplayName, user.Username);
            var avatarUrl = ZkouskaHelper.GetAvatarUrl(user.GetAvatarUrl(), user.GetDefaultAvatarUrl());
            var notificationEmbed = createEmbed(displayName, avatarUrl, user.Id);

            if (hasReacted && _state.UserReactionToThreadMessageId.TryGetValue(key, out var existingMessageId))
            {
                await UpdateOrRecreateReactionMessageAsync(thread, existingMessageId, notificationEmbed, key, displayName, user.Username, reactionType);
            }
            else
            {
                await CreateNewReactionMessageAsync(thread, notificationEmbed, key, reactedUsers!, displayName, user.Username, reactionType);
            }

            await message.RemoveReactionAsync(new Emoji(emoji), user);
            _logger.LogInformation("🧹 Removed reaction from {DisplayName} to hide count", displayName);

            return true;
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Error processing reaction");
            return false;
        }
    }

    private async Task<IThreadChannel?> GetThreadAsync(ulong threadId)
    {
        if (await _client.GetChannelAsync(threadId) is not IThreadChannel thread)
        {
            _logger.LogError("❌ Could not find thread!");
            return null;
        }

        return thread;
    }

    private bool HasUserReacted(ulong messageId, ulong userId, out HashSet<ulong>? reactedUsers)
    {
        if (_state.ZkouskaMessageToUserReactions.TryGetValue(messageId, out reactedUsers))
        {
            return reactedUsers.Contains(userId);
        }

        return false;
    }

    private async Task UpdateOrRecreateReactionMessageAsync(
        IThreadChannel thread,
        ulong existingMessageId,
        Embed notificationEmbed,
        (ulong MessageId, ulong UserId) key,
        string displayName,
        string username,
        ZkouskaReactionType reactionType)
    {
        if (await thread.GetMessageAsync(existingMessageId) is IUserMessage existingMessage)
        {
            await existingMessage.ModifyAsync(props => props.Embed = notificationEmbed);
            _logger.LogInformation("🔄 {DisplayName} ({Username}) změnil reakci na {ReactionType}", 
                displayName, username, GetReactionDisplayName(reactionType));
        }
        else
        {
            var newMessage = await thread.SendMessageAsync(embed: notificationEmbed);
            _state.UserReactionToThreadMessageId[key] = newMessage.Id;
            _logger.LogInformation("📩 {DisplayName} ({Username}) - {ReactionType} (zpráva znovu vytvořena)", 
                displayName, username, GetReactionDisplayName(reactionType));
        }
    }

    private async Task CreateNewReactionMessageAsync(
        IThreadChannel thread,
        Embed notificationEmbed,
        (ulong MessageId, ulong UserId) key,
        HashSet<ulong> reactedUsers,
        string displayName,
        string username,
        ZkouskaReactionType reactionType)
    {
        var newMessage = await thread.SendMessageAsync(embed: notificationEmbed);
        _state.UserReactionToThreadMessageId[key] = newMessage.Id;
        reactedUsers.Add(key.UserId);
        
        _logger.LogInformation("📩 {DisplayName} ({Username}) - {ReactionType}", 
            displayName, username, GetReactionDisplayName(reactionType));
    }

    private static string GetReactionDisplayName(ZkouskaReactionType reactionType)
    {
        return reactionType switch
        {
            ZkouskaReactionType.Absence => "omluvenka",
            ZkouskaReactionType.Late => "pozdní příchod",
            _ => reactionType.ToString()
        };
    }

    private async Task DeleteUserReactionMessageAsync(ulong threadId, ulong messageId)
    {
        if (await _client.GetChannelAsync(threadId) is IThreadChannel thread 
            && await thread.GetMessageAsync(messageId) is IUserMessage existingMessage)
        {
          await existingMessage.DeleteAsync();
        }
    }

    private void CleanupUserReaction((ulong MessageId, ulong UserId) key, HashSet<ulong>? reactedUsers)
    {
        _state.UserReactionToThreadMessageId.TryRemove(key, out _);
        reactedUsers?.Remove(key.UserId);
    }

    private void CleanupZkouskaState(ulong messageId)
    {
        _state.ZkouskaMessageIdToThreadId.Remove(messageId, out _);
        _state.ZkouskaMessageToUserReactions.Remove(messageId, out _);
        
        var keysToRemove = _state.UserReactionToThreadMessageId.Keys
            .Where(key => key.MessageId == messageId)
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            _state.UserReactionToThreadMessageId.TryRemove(key, out _);
        }
    }

    private async Task CloseZkouskaThreadAsync(ulong threadId, SocketGuildUser member, IUser user)
    {
        var thread = await _client.GetChannelAsync(threadId) as IThreadChannel;
        if (thread != null)
        {
            await SendClosingMessageAsync(thread, member, user);
            await thread.ModifyAsync(props => props.Archived = true);
            _logger.LogInformation("🔒 Archived thread {ThreadId}", threadId);
        }
    }

    private async Task SendClosingMessageAsync(IThreadChannel thread, SocketGuildUser member, IUser user)
    {
        var displayName = ZkouskaHelper.GetDisplayName(member.DisplayName, user.Username);
        var avatarUrl = ZkouskaHelper.GetAvatarUrl(user.GetAvatarUrl(), user.GetDefaultAvatarUrl());

        var closingEmbed = ZkouskaMessageBuilder.CreateClosingEmbed(displayName, avatarUrl);
        await thread.SendMessageAsync(embed: closingEmbed);

        _logger.LogInformation("📝 Posted closing message to thread {ThreadId}", thread.Id);
    }

    private static bool IsModeratorInChannel(SocketGuildUser? member, IMessageChannel channel)
    {
        if (member == null || channel is not SocketGuildChannel guildChannel)
        {
            return false;
        }

        var channelPermissions = member.GetPermissions(guildChannel);
        return channelPermissions.ManageMessages;
    }
}
