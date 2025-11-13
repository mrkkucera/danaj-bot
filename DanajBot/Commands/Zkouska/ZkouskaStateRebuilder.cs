using DanajBot.Settings;
using Discord;
using Discord.WebSocket;

namespace DanajBot.Commands.Zkouska;

/// <summary>
/// Handles state rebuilding from Discord history
/// </summary>
internal class ZkouskaStateRebuilder
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ZkouskaStateRebuilder> _logger;
    private readonly ZkouskaSettings _settings;
    private readonly ZkouskaState _state;

    public ZkouskaStateRebuilder(
        DiscordSocketClient client,
        ZkouskaSettings settings,
        ZkouskaState state,
        ILogger<ZkouskaStateRebuilder> logger)
    {
        _client = client;
        _logger = logger;
        _settings = settings;
        _state = state;
    }

    /// <summary>
    /// Rebuilds the state of zkouska messages from Discord history
    /// </summary>
    public async Task RebuildAsync()
    {
        try
        {
            _logger.LogInformation("🔄 Rebuilding zkouska state from Discord...");

            var sourceChannel = await GetSourceChannelAsync();
            var destinationChannel = await GetDestinationChannelAsync();

            if (sourceChannel == null || destinationChannel == null)
            {
              return;
            }

            var zkouskaMessages = await FetchZkouskaMessagesAsync(sourceChannel);
            var activeThreads = await destinationChannel.GetActiveThreadsAsync();

            _logger.LogInformation("📋 Found {ZkouskaCount} zkouska messages", zkouskaMessages.Count);
            _logger.LogInformation("🧵 Found {ThreadCount} threads", activeThreads.Count);

            var state = await MatchMessagesWithThreadsAsync(zkouskaMessages, activeThreads);


            _state.ZkouskaMessageToUserReactions.Clear();
            _state.ZkouskaMessageIdToThreadId.Clear();

            foreach (var kvp in state.MessageToThread)
            {
              _state.ZkouskaMessageIdToThreadId[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in state.ZkouskaReactions)
            {
              _state.ZkouskaMessageToUserReactions[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Error rebuilding zkouska state from Discord");
        }
    }

    /// <summary>
    /// Fetches the source channel
    /// </summary>
    private async Task<IMessageChannel?> GetSourceChannelAsync()
    {
        var channel = await _client.GetChannelAsync(_settings.SourceChannelId) as IMessageChannel;
        if (channel == null)
        {
            _logger.LogError("❌ Could not find source channel!");
        }
        return channel;
    }

    /// <summary>
    /// Fetches the destination channel
    /// </summary>
    private async Task<ITextChannel?> GetDestinationChannelAsync()
    {
        var channel = await _client.GetChannelAsync(_settings.DestinationChannelId) as ITextChannel;
        if (channel == null)
        {
            _logger.LogError("❌ Could not find destination channel!");
        }
        return channel;
    }

    /// <summary>
    /// Fetches zkouska messages from source channel
    /// </summary>
    private async Task<List<IMessage>> FetchZkouskaMessagesAsync(IMessageChannel sourceChannel)
    {
        var messages = await sourceChannel.GetMessagesAsync().FlattenAsync();

        return messages.Where(msg =>
            msg.Author.Id == _client.CurrentUser.Id &&
            ZkouskaHelper.IsZkouskaMessage(msg.Content)
        ).ToList();
    }

    /// <summary>
    /// Matches zkouska messages with their threads and rebuilds state
    /// </summary>
    private async Task<(Dictionary<ulong, ulong> MessageToThread, Dictionary<ulong, HashSet<ulong>> ZkouskaReactions)> MatchMessagesWithThreadsAsync(
        List<IMessage> zkouskaMessages,
        IReadOnlyCollection<IThreadChannel> activeThreads)
    {
        var messageToThread = new Dictionary<ulong, ulong>();
        var userReactions = new Dictionary<ulong, HashSet<ulong>>();
        int rebuiltCount = 0;

        foreach (var message in zkouskaMessages)
        {
            var zkouskaId = ZkouskaHelper.ExtractZkouskaId(message.Content);
            if (zkouskaId == null)
            {
                _logger.LogWarning("⚠️ Could not extract zkouska ID from message {MessageId}", message.Id);
                continue;
            }

            var expectedThreadSuffix = $" {zkouskaId}";
            var matchingThread = activeThreads.FirstOrDefault(thread => thread.Name.EndsWith(expectedThreadSuffix));

            if (matchingThread != null)
            {
                messageToThread[message.Id] = matchingThread.Id;
                userReactions[message.Id] = await ExtractUserReactionsAsync(matchingThread);
                rebuiltCount++;

                _logger.LogInformation("✅ Rebuilt zkouska {ZkouskaId}", zkouskaId);
                _logger.LogInformation("🧵 Thread: {ThreadName}", matchingThread.Name);
            }
            else
            {
                _logger.LogWarning("⚠️ Could not find thread for zkouska {ZkouskaId}", zkouskaId);
            }
        }

        _logger.LogInformation("✅ Rebuilt state for {RebuiltCount} zkouska messages", rebuiltCount);
        return (messageToThread, userReactions);
    }

    /// <summary>
    /// Extracts user reactions from thread messages
    /// </summary>
    private async Task<HashSet<ulong>> ExtractUserReactionsAsync(IThreadChannel thread)
    {
        var reactedUserIds = new HashSet<ulong>();

        try
        {
            var threadMessages = await thread.GetMessagesAsync().FlattenAsync();

            // Extract user IDs from embeds in thread messages
            foreach (var embed in threadMessages.SelectMany(threadMsg => threadMsg.Embeds))
            {
                var footerText = embed.Footer?.Text;

                // Skip creator messages
                if (ZkouskaHelper.IsCreatorMessage(footerText))
                    continue;

                // Extract user ID from absence messages
                if (ZkouskaHelper.TryExtractUserIdFromFooter(footerText, out var userId))
                {
                    reactedUserIds.Add(userId);
                }
            }

            _logger.LogInformation("✅ Found {ReactionCount} previous reactions", reactedUserIds.Count);
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "⚠️ Could not fetch thread messages for {ThreadName}", thread.Name);
        }

        return reactedUserIds;
    }
}
