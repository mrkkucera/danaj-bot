using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using DanajBot.Settings;
using Microsoft.Extensions.Logging;

namespace DanajBot.Commands;

/// <summary>
/// Handles the !zkouska command for creating exam announcements
/// </summary>
internal class ZkouskaCommand : ICommand
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ZkouskaCommand> _logger;
    private readonly ulong _sourceChannelId;
    private readonly ulong _destinationChannelId;
    
    // Store class message IDs to track reactions and their associated thread IDs
    private readonly ConcurrentDictionary<ulong, ulong> _classMessages = new();
    
    // Track users who have already reacted to each zkouska
    private readonly ConcurrentDictionary<ulong, HashSet<ulong>> _userReactions = new();

    public string CommandName => "!zkouska";

    public ZkouskaCommand(
        DiscordSocketClient client,
        ILogger<ZkouskaCommand> logger,
        ZkouskaSettings settings)
    {
        _client = client;
        _logger = logger;
        _sourceChannelId = settings.SourceChannelId;
        _destinationChannelId = settings.DestinationChannelId;
    }

    public async Task<bool> HandleAsync(SocketMessage message)
    {
        // Check if message is in the source channel
        if (message.Channel.Id != _sourceChannelId)
            return false;

        // Check if this is our command
        if (!message.Content.StartsWith($"{CommandName} "))
            return false;

        var guildUser = message.Author as SocketGuildUser;
        
        // Check if user has moderator permissions (MANAGE_MESSAGES)
        if (guildUser == null || !guildUser.GuildPermissions.ManageMessages)
        {
            await message.Channel.SendMessageAsync("🔒 Potřebujete moderátorské oprávnění k vytvoření zkoušky!");
            return true;
        }

        // Extract class description
        var description = message.Content.Substring(CommandName.Length + 1).Trim();
        
        if (string.IsNullOrEmpty(description))
        {
            await message.Channel.SendMessageAsync($"⚠️ Chybí popis! Použití: `{CommandName} <description>`");
            return true;
        }

        try
        {
            // Get the destination channel
            var destinationChannel = await _client.GetChannelAsync(_destinationChannelId) as ITextChannel;

            if (destinationChannel == null)
            {
                _logger.LogError("❌ Could not find destination channel!");
                await message.Channel.SendMessageAsync("❌ Chyba: Nelze najít cílový kanál.");
                return true;
            }

            // Create a regular message for the class
            var classMessageText = $"📝 **Zkouška**\n\n{description}\n\n*Reagujte pomocí ❌ pokud se chcete omluvit z této zkoušky. Vaše reakce po chvilce zmizí, ale bude zaznamenána.*";

            // Send the class message
            var classMessage = await message.Channel.SendMessageAsync(classMessageText);
            
            // Add the default reaction
            await classMessage.AddReactionAsync(new Emoji("❌"));
            
            // Create a thread in the destination channel for this zkouska
            var threadName = description.Length > 100 ? $"{description.Substring(0, 97)}..." : description;
            var thread = await destinationChannel.CreateThreadAsync(
                $"📝 {threadName}",
                ThreadType.PublicThread,
                ThreadArchiveDuration.OneWeek
            );
            
            // Store the message ID and thread ID mapping
            _classMessages[classMessage.Id] = thread.Id;
            
            // Initialize the user reactions set for this zkouska
            _userReactions[classMessage.Id] = new HashSet<ulong>();
            
            // Delete the command message
            await message.DeleteAsync();
            
            _logger.LogInformation("✅ {Username} vytvoril zkousku: {Description}", message.Author.Username, description);
            _logger.LogInformation("🧵 Created thread: {ThreadName} ({ThreadId})", thread.Name, thread.Id);
            
            return true;
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Chyba pri vytvareni zkousky");
            await message.Channel.SendMessageAsync("❌ Chyba pri vytvareni zkousky. Zkuste to prosím znovu.");
            return true;
        }
    }
    
    /// <summary>
    /// Rebuilds the state of zkouska messages from Discord history
    /// </summary>
    public async Task RebuildStateAsync()
    {
        try
        {
            _logger.LogInformation("🔄 Rebuilding zkouska state from Discord...");
            
            // Fetch the source channel
            var sourceChannel = await _client.GetChannelAsync(_sourceChannelId) as IMessageChannel;
            if (sourceChannel == null)
            {
                _logger.LogError("❌ Could not find source channel!");
                return;
            }

            // Fetch the destination channel
            var destinationChannel = await _client.GetChannelAsync(_destinationChannelId) as ITextChannel;
            if (destinationChannel == null)
            {
                _logger.LogError("❌ Could not find destination channel!");
                return;
            }

            // Fetch recent messages from source channel
            var messages = await sourceChannel.GetMessagesAsync(100).FlattenAsync();
            
            // Find zkouska messages
            var zkouskaMessages = messages.Where(msg => 
                msg.Author.Id == _client.CurrentUser.Id &&
                msg.Content.StartsWith("📝 **Zkouška**")
            ).ToList();

            // Fetch all threads from destination channel
            var activeThreads = await destinationChannel.GetActiveThreadsAsync();

            _logger.LogInformation("📋 Found {ZkouskaCount} zkouska messages", zkouskaMessages.Count);
            _logger.LogInformation("🧵 Found {ThreadCount} threads", activeThreads.Count);

            int rebuiltCount = 0;

            // Match zkouska messages with their threads
            foreach (var message in zkouskaMessages)
            {
                // Extract description from message
                var content = message.Content;
                var startIndex = content.IndexOf("📝 **Zkouška**\n\n") + "📝 **Zkouška**\n\n".Length;
                var endIndex = content.IndexOf("\n\n*");
                
                if (startIndex < 0 || endIndex < 0) continue;
                
                var description = content.Substring(startIndex, endIndex - startIndex);
                var threadName = description.Length > 100 ? $"{description.Substring(0, 97)}..." : description;
                var expectedThreadName = $"📝 {threadName}";

                // Find matching thread
                var matchingThread = activeThreads.FirstOrDefault(thread => thread.Name == expectedThreadName);

                if (matchingThread != null)
                {
                    // Store the mapping
                    _classMessages[message.Id] = matchingThread.Id;
                    
                    // Initialize user reactions set
                    var reactedUserIds = new HashSet<ulong>();
                    
                    // Fetch messages from the thread to rebuild user reactions
                    try
                    {
                        var threadMessages = await matchingThread.GetMessagesAsync(100).FlattenAsync();
                        
                        // Extract user IDs from embeds in the thread
                        foreach (var embeds in threadMessages.Select(m => m.Embeds))
                        {
                            if (embeds.Count > 0)
                            {
                                var embed = embeds.First();
                                var footerText = embed.Footer?.Text;
                                if (footerText != null && footerText.StartsWith("User ID: "))
                                {
                                    var userIdStr = footerText.Substring("User ID: ".Length);
                                    if (ulong.TryParse(userIdStr, out var userId))
                                    {
                                        reactedUserIds.Add(userId);
                                    }
                                }
                            }
                        }
                        
                        _logger.LogInformation("✅ Found {ReactionCount} previous reactions", reactedUserIds.Count);
                    }
                    catch (Exception error)
                    {
                        _logger.LogWarning(error, "⚠️ Could not fetch thread messages for {ThreadName}", matchingThread.Name);
                    }
                    
                    _userReactions[message.Id] = reactedUserIds;
                    rebuiltCount++;
                    
                    var shortDesc = description.Length > 50 ? $"{description.Substring(0, 50)}..." : description;
                    _logger.LogInformation("✅ Rebuilt: {Description}", shortDesc);
                    _logger.LogInformation("🧵 Thread: {ThreadName}", matchingThread.Name);
                }
            }

            _logger.LogInformation("✅ Rebuilt state for {RebuiltCount} zkouska messages", rebuiltCount);
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Error rebuilding zkouska state from Discord");
        }
    }

    /// <summary>
    /// Handles reactions to zkouska messages
    /// </summary>
    public async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        // Ignore bot reactions
        if (reaction.User.IsSpecified && reaction.User.Value.IsBot) return;

        // Get the message
        var message = await cache.GetOrDownloadAsync();
        if (message == null) return;

        // Check if this is a class message we're tracking
        if (!_classMessages.TryGetValue(message.Id, out var threadId)) return;

        // Check if the reaction is ?
        if (reaction.Emote.Name != "❌") return;

        try
        {
            // Get the user
            var user = reaction.User.IsSpecified ? reaction.User.Value : await _client.GetUserAsync(reaction.UserId);
            if (user == null) return;

            // Check if this user has already reacted to this zkouska
            if (_userReactions.TryGetValue(message.Id, out var reactedUsers) && reactedUsers.Contains(user.Id))
            {
                _logger.LogWarning("⚠️ {Username} has already reacted to this zkouska, skipping...", user.Username);
                // Still remove the reaction to keep the UI clean
                await message.RemoveReactionAsync(reaction.Emote, user);
                return;
            }

            // Fetch the thread
            var thread = await _client.GetChannelAsync(threadId) as IThreadChannel;

            if (thread == null)
            {
                _logger.LogError("❌ Could not find thread!");
                return;
            }

            // Get the member who reacted
            var guild = (message.Channel as SocketGuildChannel)?.Guild;
            var member = guild?.GetUser(user.Id);
            var displayName = member?.DisplayName ?? user.Username;

            // Get the class description from the message content
            var messageContent = message.Content;
            var startIndex = messageContent.IndexOf("📝 **Zkouška**\n\n") + "📝 **Zkouška**\n\n".Length;
            var endIndex = messageContent.IndexOf("\n\n*");
            var classDescription = (startIndex >= 0 && endIndex >= 0) 
                ? messageContent.Substring(startIndex, endIndex - startIndex) 
                : "Unknown class";

            // Create an embed to send to the thread
            var notificationEmbed = new EmbedBuilder()
                .WithAuthor(displayName, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithDescription($"**Omlouvenka ze zkoušky:**\n{classDescription}")
                .WithColor(Discord.Color.Blue)
                .WithCurrentTimestamp()
                .WithFooter($"User ID: {user.Id}")
                .Build();

            // Send to the thread
            await thread.SendMessageAsync(embed: notificationEmbed);
            
            // Mark this user as having reacted
            if (reactedUsers != null)
            {
                reactedUsers.Add(user.Id);
            }
            
            _logger.LogInformation("📩 {DisplayName} ({Username}) se omluvil ze zkousky", displayName, user.Username);

            // Remove the user's reaction to restore the default state
            await message.RemoveReactionAsync(reaction.Emote, user);
            
            _logger.LogInformation("🧹 Removed reaction from {DisplayName} to hide count", displayName);
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Error processing reaction");
        }
    }
}
