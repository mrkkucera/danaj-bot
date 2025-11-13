using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using DanajBot.Settings;

namespace DanajBot.Commands;

/// <summary>
/// Handles the !zkouska command for creating zkouska announcements
/// </summary>
internal class ZkouskaCommand : ICommand
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ZkouskaCommand> _logger;
    private readonly ulong _sourceChannelId;
    private readonly ulong _destinationChannelId;
    
    // Store zkouska message IDs to track reactions and their associated thread IDs
    private readonly ConcurrentDictionary<ulong, ulong> _zkouskaMessages = new();
    
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

        // Extract zkouska description
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

            // Generate a unique ID for this zkouska
            var zkouskaId = Guid.NewGuid().ToString("N")[..8]; // Use first 8 characters for shorter ID

            // Create a regular message for the zkouska
            var zkouskaMessageText = $"📝 **Zkouška** `#{zkouskaId}`\n\n{description}\n\n*Reagujte pomocí ❌ pokud se chcete omluvit z této zkoušky. Vaše reakce po chvilce zmizí, ale bude zaznamenána.*\n*Moderátoři můžou reagovat pomocí 🗑️, aby uzavřeli omluvenky na tuto zkoušku.*";

            // Send the zkouska message
            var zkouskaMessage = await message.Channel.SendMessageAsync(zkouskaMessageText);
            
            // Add the default reactions
            await zkouskaMessage.AddReactionAsync(new Emoji("❌"));
            await zkouskaMessage.AddReactionAsync(new Emoji("🗑️"));
            
            // Create a thread in the destination channel for this zkouska
            var threadName = description.Length > 100 ? $"{description.Substring(0, 97)}..." : description;
            var thread = await destinationChannel.CreateThreadAsync(
                $"{threadName} #{zkouskaId}",
                ThreadType.PublicThread,
                ThreadArchiveDuration.OneWeek
            );
            
            // Store the message ID and thread ID mapping
            _zkouskaMessages[zkouskaMessage.Id] = thread.Id;
            
            // Initialize the user reactions set for this zkouska
            _userReactions[zkouskaMessage.Id] = new HashSet<ulong>();
            
            // Send creator info to the thread
            var displayName = guildUser.DisplayName ?? guildUser.Username;
            var creatorEmbed = new EmbedBuilder()
                .WithAuthor(displayName, guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl())
                .WithDescription($"**Zkouška vytvořena:**\n{description}")
                .WithColor(Discord.Color.Green)
                .WithCurrentTimestamp()
                .WithFooter($"Zkouška ID: {zkouskaId} | User ID: {guildUser.Id}")
                .Build();
            
            await thread.SendMessageAsync(embed: creatorEmbed);
            
            // Delete the command message
            await message.DeleteAsync();
            
            _logger.LogInformation("✅ {Username} vytvoril zkousku #{ZkouskaId}: {Description}", message.Author.Username, zkouskaId, description);
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

            // Match zkouska messages with their threads using zkouska ID
            foreach (var message in zkouskaMessages)
            {
                // Extract zkouska ID from message
                var content = message.Content;
                var match = Regex.Match(content, @"`(#[a-fA-F0-9]{8})`");
                
                if (!match.Success) 
                {
                    _logger.LogWarning("⚠️ Could not extract zkouska ID from message {MessageId}", message.Id);
                    continue;
                }

                var zkouskaId = match.Groups[1].Value;
                var expectedThreadSuffix = $" {zkouskaId}";

                // Find matching thread by ID suffix
                var matchingThread = activeThreads.FirstOrDefault(thread => thread.Name.EndsWith(expectedThreadSuffix));

                if (matchingThread != null)
                {
                    // Store the mapping
                    _zkouskaMessages[message.Id] = matchingThread.Id;
                    
                    // Initialize user reactions set
                    var reactedUserIds = new HashSet<ulong>();
                    
                    // Fetch messages from the thread to rebuild user reactions
                    try
                    {
                        var threadMessages = await matchingThread.GetMessagesAsync(100).FlattenAsync();
                        
                        // Extract user ID from embeds in the thread (exclude creator message)
                        foreach (var threadMsg in threadMessages)
                        {
                            if (threadMsg.Embeds.Count > 0)
                            {
                                var embed = threadMsg.Embeds.First();
                                var footerText = embed.Footer?.Text;
                                
                                // Skip creator messages (they contain "Zkouška ID:")
                                if (footerText != null && footerText.Contains("Zkouška ID:"))
                                    continue;
                                
                                // Extract user ID from absence messages
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
                    
                    _logger.LogInformation("✅ Rebuilt zkouska #{ZkouskaId}", zkouskaId);
                    _logger.LogInformation("🧵 Thread: {ThreadName}", matchingThread.Name);
                }
                else
                {
                    _logger.LogWarning("⚠️ Could not find thread for zkouska #{ZkouskaId}", zkouskaId);
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

        // Check if this is a zkouska message we're tracking
        if (!_zkouskaMessages.TryGetValue(message.Id, out var threadId)) return;

        // Get the user
        var user = reaction.User.IsSpecified ? reaction.User.Value : await _client.GetUserAsync(reaction.UserId);
        if (user == null) return;

        // Get the guild and member info
        var guild = (message.Channel as SocketGuildChannel)?.Guild;
        var member = guild?.GetUser(user.Id);

        // Check if the reaction is 🗑️ (trashcan) for moderators to delete and close
        if (reaction.Emote.Name == "🗑️")
        {
            // Check if user has moderator permissions
            if (member != null && member.GuildPermissions.ManageMessages)
            {
                try
                {
                    _logger.LogInformation("🗑️ {Username} is deleting zkouska message {MessageId}", user.Username, message.Id);

                    // Extract zkouska ID from message content
                    var messageContent = message.Content;
                    var match = Regex.Match(messageContent, @"`(#[a-fA-F0-9]{8})`");
                    var zkouskaId = match.Success ? match.Groups[1].Value : "Unknown";

                    // Fetch the thread
                    var thread = await _client.GetChannelAsync(threadId) as IThreadChannel;

                    if (thread != null)
                    {
                        var displayName = member.DisplayName ?? user.Username;
                        
                        // Post closing message to the thread
                        var closingEmbed = new EmbedBuilder()
                            .WithAuthor(displayName, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                            .WithDescription("🔒 **Příjem omluvenek uzavřen**\n\nTato zkouška byla uzavřena moderátorem.")
                            .WithColor(Discord.Color.Red)
                            .WithCurrentTimestamp()
                            .Build();

                        await thread.SendMessageAsync(embed: closingEmbed);
                        
                        _logger.LogInformation("📝 Posted closing message to thread {ThreadId}", threadId);

                        // Archive (close) the thread
                        await thread.ModifyAsync(props => props.Archived = true);
                        
                        _logger.LogInformation("🔒 Archived thread {ThreadId}", threadId);
                    }

                    // Delete the message from the source channel
                    await message.DeleteAsync();
                    
                    // Remove from tracking
                    _zkouskaMessages.TryRemove(message.Id, out _);
                    _userReactions.TryRemove(message.Id, out _);
                    
                    _logger.LogInformation("✅ {Username} successfully deleted zkouska {ZkouskaId}", user.Username, zkouskaId);
                }
                catch (Exception error)
                {
                    _logger.LogError(error, "❌ Error deleting zkouska message");
                }
            }
            else
            {
                // User is not a moderator, just remove the reaction
                _logger.LogWarning("⚠️ {Username} attempted to delete zkouska but lacks moderator permissions", user.Username);
                await message.RemoveReactionAsync(reaction.Emote, user);
            }
            
            return;
        }

        // Check if the reaction is ❌
        if (reaction.Emote.Name != "❌")
        {
            // Remove any other emoji reactions
            await message.RemoveReactionAsync(reaction.Emote, user);
            return;
        }

        try
        {
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

            var displayName = member?.DisplayName ?? user.Username;

            // Create an embed to send to the thread
            var notificationEmbed = new EmbedBuilder()
                .WithAuthor(displayName, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithDescription($"**Omluvenka ze zkoušky**")
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
