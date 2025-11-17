using DanajBot.Commands;
using DanajBot.Commands.Zkouska;
using DanajBot.Services;
using DanajBot.Settings;
using Discord;
using Discord.WebSocket;
using System.Text;

// Enable UTF-8 encoding for console output (fixes emoji display in terminals)
Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

// Configure settings from AppSettings section
var settings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>()!;
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(settings.Zkouska);
builder.Services.AddSingleton(settings.EveryonePermissionChecksSettings);
builder.Services.AddSingleton<ZkouskaState>();
// Configure Discord client
builder.Services.AddSingleton<DiscordSocketClient>(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds |
                     GatewayIntents.GuildMessages |
                     GatewayIntents.MessageContent |
                     GatewayIntents.GuildMessageReactions
}));
builder.Services.AddSingleton<ZkouskaStateRebuilder>();
builder.Services.AddSingleton<ZkouskaReactionHandler>();

// Register commands
builder.Services.AddSingleton<ICommand, ZkouskaCommand>();

// Register command handler
builder.Services.AddSingleton<CommandHandler>();

// Register services
builder.Services.AddSingleton<BotService>();
builder.Services.AddHostedService<BotHostedService>();

// Configure web server for health endpoint
builder.Services.AddSingleton<WebServerHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebServerHostedService>());

// Configure permission checker service
builder.Services.AddHostedService<PermissionCheckerService>();

var host = builder.Build();
await host.RunAsync();
