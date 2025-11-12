using Discord;
using Discord.WebSocket;
using DanajBot.Commands;
using DanajBot.Services;
using DanajBot.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure settings from AppSettings section
var settings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>()!;
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(settings.Zkouska);

// Configure Discord client
builder.Services.AddSingleton<DiscordSocketClient>(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds |
                     GatewayIntents.GuildMessages |
                     GatewayIntents.MessageContent |
                     GatewayIntents.GuildMessageReactions
}));

// Register commands
builder.Services.AddSingleton<ICommand>(sp =>
{
    var client = sp.GetRequiredService<DiscordSocketClient>();
    var logger = sp.GetRequiredService<ILogger<ZkouskaCommand>>();
    var zkSettings = sp.GetRequiredService<ZkouskaSettings>();
    
    return new ZkouskaCommand(client, logger, zkSettings);
});

// Register command handler
builder.Services.AddSingleton<CommandHandler>();

// Register services
builder.Services.AddSingleton<BotService>();
builder.Services.AddHostedService<BotHostedService>();

// Configure health checks
builder.Services.AddHealthChecks()
    .AddCheck<HealthCheckService>("discord_bot");

// Configure web server for health endpoint
builder.Services.AddSingleton<WebServerHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebServerHostedService>());

var host = builder.Build();
await host.RunAsync();
