using Discord.WebSocket;

namespace DanajBot.Commands;

internal class HelpCommand : ICommand
{
    private readonly IServiceProvider _services;

    public string CommandName => "!help";
    public string Usage => "!help — Zobrazí dostupné příkazy a jejich použití";

    public HelpCommand(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<bool> HandleAsync(SocketMessage message)
    {
        if (message.Author.IsBot)
        {
            return false;
        }

        if (!message.Content.StartsWith(CommandName))
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
