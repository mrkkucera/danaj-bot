using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DanajBot.Commands;

internal class HelpCommand : ICommand
{
    private readonly IServiceProvider _services;
    private readonly Settings.AppSettings _settings;

    public string CommandName => "!help";
    public string Usage => "!help — Zobrazí dostupné příkazy a jejich použití";

    public HelpCommand(IServiceProvider services, Settings.AppSettings settings)
    {
        _services = services;
        _settings = settings;
    }

    public async Task<bool> HandleAsync(SocketMessage message)
    {
        if (message.Author.IsBot)
        {
            return false;
        }

        if (message.Channel.Id != _settings.BotChatChannelId || !message.Content.StartsWith("!help"))
        {
            return false;
        }

        var commands = GetVisibleCommands();
        var helpMessage = BuildHelpMessage(commands);

        await message.Channel.SendMessageAsync(helpMessage);
        await message.DeleteAsync();
        
        return true;
    }

    private IEnumerable<ICommand> GetVisibleCommands()
    {
        var commands = _services.GetServices<ICommand>();

        return commands
            .OrderBy(c => c.CommandName);
    }

    internal static string BuildHelpMessage(IEnumerable<ICommand> commands)
    {
        var lines = new List<string>();
        lines.Add("📝 Dostupné příkazy:");

        foreach (var cmd in commands)
        {
            var usage = string.IsNullOrWhiteSpace(cmd.Usage) ? cmd.CommandName : cmd.Usage;
            lines.Add($"▸ `{usage}`");
        }

        return string.Join('\n', lines);
    }
}
