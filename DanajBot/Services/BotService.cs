using DanajBot.Commands;
using DanajBot.Commands.Zkouska;
using Discord;
using Discord.WebSocket;

namespace DanajBot.Services;

internal class BotService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<BotService> _logger;
    private readonly CommandHandler _commandHandler;

    public BotService(
        DiscordSocketClient client, 
        CommandHandler commandHandler,
        ILogger<BotService> logger)
    {
        _client = client;
        _commandHandler = commandHandler;
        _logger = logger;
        
        // Subscribe to events
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;
        _client.ReactionAdded += ReactionAddedAsync;
    }

    private Task LogAsync(LogMessage log)
    {
        var logLevel = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };
        
        _logger.Log(logLevel, log.Exception, "{Source}: {Message}", log.Source, log.Message);
        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
        _logger.LogInformation("🤖 Bot is online!");
        _logger.LogInformation("👤 Logged in as: {Username}#{Discriminator}", 
            _client.CurrentUser.Username, _client.CurrentUser.Discriminator);
        _logger.LogInformation("-----------------------------------");
        
        // Rebuild state from existing Discord messages
        await RebuildStateFromDiscordAsync();
    }

    private async Task RebuildStateFromDiscordAsync()
    {
        try
        {
            _logger.LogInformation("🔄 Rebuilding state from Discord...");
            
            // Delegate to zkouska command to rebuild its state
            var zkouskaCommand = _commandHandler.GetCommand<ZkouskaCommand>();
            if (zkouskaCommand != null)
            {
                await zkouskaCommand.RebuildStateAsync();
            }
            
            _logger.LogInformation("-----------------------------------");
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Error rebuilding state from Discord");
        }
    }

    private async Task MessageReceivedAsync(SocketMessage message)
    {
        // Delegate to command handler
        await _commandHandler.HandleMessageAsync(message);
    }

    private async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        // Delegate to zkouska command for reaction handling
        var zkouskaCommand = _commandHandler.GetCommand<ZkouskaCommand>();
        if (zkouskaCommand != null)
        {
            await zkouskaCommand.HandleReactionAsync(cache, channel, reaction);
        }
    }
}
