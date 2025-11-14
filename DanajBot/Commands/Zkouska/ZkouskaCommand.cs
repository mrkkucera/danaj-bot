using DanajBot.Settings;
using Discord;
using Discord.WebSocket;

namespace DanajBot.Commands.Zkouska;

/// <summary>
/// Handles the !zkouska command for creating zkouska announcements
/// </summary>
internal class ZkouskaCommand : ICommand
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ZkouskaCommand> _logger;
    private readonly ZkouskaStateRebuilder _stateRebuilder;
    private readonly ZkouskaReactionHandler _reactionHandler;
    private readonly ZkouskaState _state;
    private readonly ZkouskaSettings _settings;

    public string CommandName => "!zkouska";

    public ZkouskaCommand(
        DiscordSocketClient client,
        ILogger<ZkouskaCommand> logger,
        ZkouskaStateRebuilder stateRebuilder,
        ZkouskaReactionHandler reactionHandler,
        ZkouskaState state,
        ZkouskaSettings settings)
    {
        _client = client;
        _logger = logger;
        _stateRebuilder = stateRebuilder;
        _reactionHandler = reactionHandler;
        _state = state;
        _settings = settings;
    }

    public async Task<bool> HandleAsync(SocketMessage message)
    {
        if (!IsValidCommand(message))
            return false;

        var guildUser = message.Author as SocketGuildUser;
        
        if (!HasModeratorPermissions(guildUser, message.Channel))
        {
            await message.Channel.SendMessageAsync(ZkouskaConstants.NoPermissionMessage);
            return true;
        }

        var description = ExtractDescription(message.Content);
        if (string.IsNullOrEmpty(description))
        {
            await message.Channel.SendMessageAsync(
                string.Format(ZkouskaConstants.MissingDescriptionMessage, CommandName));
            return true;
        }

        // guildUser cannot be null here because HasModeratorPermissions ensures it
        await CreateZkouskaAnnouncementAsync(message, guildUser!, description);
        return true;
    }

    /// <summary>
    /// Rebuilds the state of zkouska messages from Discord history
    /// </summary>
    public async Task RebuildStateAsync()
    {
        await _stateRebuilder.RebuildAsync();
    }

    /// <summary>
    /// Handles reactions to zkouska messages
    /// </summary>
    public async Task HandleReactionAsync(
        Cacheable<IUserMessage, ulong> cache,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (reaction.User.IsSpecified && reaction.User.Value.IsBot)
            return;

        var message = await cache.GetOrDownloadAsync();
        if (message == null)
            return;

        if (!_state.ZkouskaMessageIdToThreadId.TryGetValue(message.Id, out var threadId))
            return;

        var user = await GetUserFromReactionAsync(reaction);
        if (user == null)
            return;

        var guild = (message.Channel as SocketGuildChannel)?.Guild;
        var member = guild?.GetUser(user.Id);

        await ProcessReactionAsync(message, reaction.Emote.Name, user, member, threadId);
    }

    private bool IsValidCommand(SocketMessage message)
    {
        return message.Channel.Id == _settings.SourceChannelId &&
               message.Content.StartsWith($"{CommandName} ");
    }

    private static bool HasModeratorPermissions(SocketGuildUser? guildUser, ISocketMessageChannel channel)
    {
        if (guildUser == null || channel is not SocketGuildChannel guildChannel)
        {
            return false;
        }

        var channelPermissions = guildUser.GetPermissions(guildChannel);
        return channelPermissions.ManageMessages;
    }

    private string ExtractDescription(string content)
    {
        return content.Substring(CommandName.Length + 1).Trim();
    }

    private async Task CreateZkouskaAnnouncementAsync(SocketMessage message, SocketGuildUser guildUser, string description)
    {
        try
        {
            var destinationChannel = await GetDestinationChannelAsync();
            if (destinationChannel == null)
            {
                await message.Channel.SendMessageAsync(ZkouskaConstants.ChannelNotFoundMessage);
                return;
            }

            var zkouskaId = ZkouskaMessageBuilder.GenerateZkouskaId();
            var zkouskaMessage = await SendZkouskaMessageAsync(message.Channel, zkouskaId, description);
            var thread = await CreateZkouskaAbsencesThreadAsync(destinationChannel, description, zkouskaId);

            _state.ZkouskaMessageIdToThreadId[zkouskaMessage.Id] = thread.Id;
            _state.ZkouskaMessageToUserReactions[zkouskaMessage.Id] = new HashSet<ulong>();

            await SendCreatorNotificationAsync(thread, guildUser, description, zkouskaId);
            await message.DeleteAsync();

            LogZkouskaCreation(message.Author.Username, zkouskaId, description, thread);
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Chyba pri vytvareni zkousky");
            await message.Channel.SendMessageAsync(ZkouskaConstants.CreateErrorMessage);
        }
    }

    private async Task<ITextChannel?> GetDestinationChannelAsync()
    {
        var channel = await _client.GetChannelAsync(_settings.DestinationChannelId) as ITextChannel;
        if (channel == null)
        {
            _logger.LogError("❌ Could not find destination channel!");
        }
        return channel;
    }

    private static async Task<IUserMessage> SendZkouskaMessageAsync(
        ISocketMessageChannel channel,
        string zkouskaId,
        string description)
    {
        var messageText = ZkouskaMessageBuilder.CreateZkouskaMessage(zkouskaId, description);
        var zkouskaMessage = await channel.SendMessageAsync(messageText);

        await zkouskaMessage.AddReactionAsync(new Emoji(ZkouskaConstants.AbsenceEmoji));
        await zkouskaMessage.AddReactionAsync(new Emoji(ZkouskaConstants.DeleteEmoji));

        return zkouskaMessage;
    }

    private static async Task<IThreadChannel> CreateZkouskaAbsencesThreadAsync(
        ITextChannel destinationChannel,
        string description,
        string zkouskaId)
    {
        var threadName = ZkouskaMessageBuilder.CreateThreadName(description, zkouskaId);
        return await destinationChannel.CreateThreadAsync(
            threadName,
            ThreadType.PublicThread,
            ThreadArchiveDuration.OneWeek);
    }

    private static async Task SendCreatorNotificationAsync(
        IThreadChannel thread,
        SocketGuildUser guildUser,
        string description,
        string zkouskaId)
    {
        var displayName = ZkouskaHelper.GetDisplayName(guildUser.DisplayName, guildUser.Username);
        var avatarUrl = ZkouskaHelper.GetAvatarUrl(guildUser.GetAvatarUrl(), guildUser.GetDefaultAvatarUrl());

        var creatorEmbed = ZkouskaMessageBuilder.CreateCreatorEmbed(
            displayName,
            avatarUrl,
            description,
            zkouskaId,
            guildUser.Id);

        await thread.SendMessageAsync(embed: creatorEmbed);
    }

    private void LogZkouskaCreation(string username, string zkouskaId, string description, IThreadChannel thread)
    {
        _logger.LogInformation(
            "✅ {Username} vytvoril zkousku #{ZkouskaId}: {Description}",
            username,
            zkouskaId,
            description);
        _logger.LogInformation("🧵 Created thread: {ThreadName} ({ThreadId})", thread.Name, thread.Id);
    }

    private async Task<IUser?> GetUserFromReactionAsync(SocketReaction reaction)
    {
        return reaction.User.IsSpecified
            ? reaction.User.Value
            : await _client.GetUserAsync(reaction.UserId);
    }

    private async Task ProcessReactionAsync(
        IUserMessage message,
        string emoteName,
        IUser user,
        SocketGuildUser? member,
        ulong threadId)
    {
        switch (emoteName)
        {
            case ZkouskaConstants.DeleteEmoji:
                if (await _reactionHandler.HandleDeleteReactionAsync(message, user, member, threadId))
                {
                    // Remove from tracking if successfully deleted
                    _state.ZkouskaMessageIdToThreadId.TryRemove(message.Id, out _);
                    _state.ZkouskaMessageToUserReactions.TryRemove(message.Id, out _);
                }
                break;

            case ZkouskaConstants.AbsenceEmoji:
                await _reactionHandler.HandleAbsenceReactionAsync(message, user, member, threadId);
                break;

            default:
                // Remove any other emoji reactions
                await message.RemoveReactionAsync(new Emoji(emoteName), user);
                break;
        }
    }
}
