using Discord;
using Discord.WebSocket;
using DanajBot.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DanajBot.Services;

internal class BotHostedService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly AppSettings _settings;
    private readonly ZkouskaSettings _zkouskaSettings;
    private readonly ILogger<BotHostedService> _logger;

    public BotHostedService(
        DiscordSocketClient client, 
        AppSettings settings,
        ZkouskaSettings zkouskaSettings,
        BotService botService,
        ILogger<BotHostedService> logger)
    {
        _client = client;
        _settings = settings;
        _zkouskaSettings = zkouskaSettings;
        _logger = logger;
        
        // BotService subscribes to events in its constructor, so we just need it to be instantiated
        _ = botService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_settings.DiscordToken))
        {
            _logger.LogCritical("❌ Error: Missing DISCORD_TOKEN in configuration!");
            throw new InvalidOperationException("DISCORD_TOKEN is required in configuration.");
        }

        if (_zkouskaSettings.SourceChannelId == 0 || _zkouskaSettings.DestinationChannelId == 0)
        {
            _logger.LogCritical("❌ Error: Missing zkouska channel configuration!");
            _logger.LogInformation("Please check your configuration and ensure all required variables are set:");
            _logger.LogInformation("- AppSettings:DiscordToken");
            _logger.LogInformation("- AppSettings:Zkouska:SourceChannelId");
            _logger.LogInformation("- AppSettings:Zkouska:DestinationChannelId");
            throw new InvalidOperationException("Zkouska channel IDs are required in configuration.");
        }

        // Login and start the client
        await _client.LoginAsync(TokenType.Bot, _settings.DiscordToken);
        await _client.StartAsync();
        
        _logger.LogInformation("Discord bot started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Discord bot...");
        await _client.StopAsync();
        _logger.LogInformation("Discord bot stopped");
    }
}
