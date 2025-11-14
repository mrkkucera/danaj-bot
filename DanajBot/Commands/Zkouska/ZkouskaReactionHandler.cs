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
        if (member == null || !HasManageMessagesInChannel(member, message.Channel))
        {
            _logger.LogWarning("⚠️ {Username} attempted to delete zkouska but lacks moderator permissions", user.Username);
            await message.RemoveReactionAsync(new Emoji(ZkouskaConstants.DeleteEmoji), user);
            return false;
        }

        try
        {
            _logger.LogInformation("🗑️ {Username} is deleting zkouska message {MessageId}", user.Username, message.Id);

            var zkouskaId = ZkouskaHelper.ExtractZkouskaId(message.Content) ?? "Unknown";

            // Fetch and close the thread
            var thread = await _client.GetChannelAsync(threadId) as IThreadChannel;
            if (thread != null)
            {
                await SendClosingMessageAsync(thread, member, user);
                await thread.ModifyAsync(props => props.Archived = true);
                _logger.LogInformation("🔒 Archived thread {ThreadId}", threadId);
            }

            // Delete the message from the source channel
            await message.DeleteAsync();

            // Remove from tracking
            _state.ZkouskaMessageIdToThreadId.Remove(message.Id, out _);
            _state.ZkouskaMessageToUserReactions.Remove(message.Id, out _);

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
        // Check if this user has already reacted
        if (_state.ZkouskaMessageToUserReactions.TryGetValue(message.Id, out var reactedUsers) && reactedUsers.Contains(user.Id))
        {
            _logger.LogWarning("⚠️ {Username} has already reacted to this zkouska, skipping...", user.Username);
            await message.RemoveReactionAsync(new Emoji(ZkouskaConstants.AbsenceEmoji), user);
            return false;
        }

        try
        {
            if (await _client.GetChannelAsync(threadId) is not IThreadChannel thread)
            {
                _logger.LogError("❌ Could not find thread!");
                return false;
            }

            var displayName = ZkouskaHelper.GetDisplayName(member?.DisplayName, user.Username);
            var avatarUrl = ZkouskaHelper.GetAvatarUrl(user.GetAvatarUrl(), user.GetDefaultAvatarUrl());

            var notificationEmbed = ZkouskaMessageBuilder.CreateAbsenceEmbed(displayName, avatarUrl, user.Id);
            await thread.SendMessageAsync(embed: notificationEmbed);

            reactedUsers?.Add(user.Id);

            _logger.LogInformation("📩 {DisplayName} ({Username}) se omluvil ze zkousky", displayName, user.Username);

            // Remove the user's reaction to restore the default state
            await message.RemoveReactionAsync(new Emoji(ZkouskaConstants.AbsenceEmoji), user);
            _logger.LogInformation("🧹 Removed reaction from {DisplayName} to hide count", displayName);

            return true;
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Error processing reaction");
            return false;
        }
    }

    private static bool HasManageMessagesInChannel(SocketGuildUser member, IMessageChannel channel)
    {
        if (channel is not SocketGuildChannel guildChannel)
        {
            return false;
        }

        var channelPermissions = member.GetPermissions(guildChannel);
        return channelPermissions.ManageMessages;
    }

    private async Task SendClosingMessageAsync(IThreadChannel thread, SocketGuildUser member, IUser user)
    {
        var displayName = ZkouskaHelper.GetDisplayName(member.DisplayName, user.Username);
        var avatarUrl = ZkouskaHelper.GetAvatarUrl(user.GetAvatarUrl(), user.GetDefaultAvatarUrl());

        var closingEmbed = ZkouskaMessageBuilder.CreateClosingEmbed(displayName, avatarUrl);
        await thread.SendMessageAsync(embed: closingEmbed);

        _logger.LogInformation("📝 Posted closing message to thread {ThreadId}", thread.Id);
    }
}
