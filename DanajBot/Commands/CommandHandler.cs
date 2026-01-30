using Discord.WebSocket;

namespace DanajBot.Commands;

/// <summary>
/// Manages and routes commands to their respective handlers
/// </summary>
internal class CommandHandler
{
    private readonly ILogger<CommandHandler> _logger;
    private readonly List<ICommand> _commands;

    public CommandHandler(IEnumerable<ICommand> commands, ILogger<CommandHandler> logger)
    {
        _commands = commands.ToList();
        _logger = logger;
        
        _logger.LogInformation("Registered {CommandCount} commands: {Commands}", 
            _commands.Count, 
            string.Join(", ", _commands.Select(c => c.CommandName)));
    }

    /// <summary>
    /// Processes a message and routes it to the appropriate command handler
    /// </summary>
    /// <param name="message">The message to process</param>
    /// <returns>True if a command handled the message, false otherwise</returns>
    public async Task<bool> HandleMessageAsync(SocketMessage message)
    {
        // Ignore messages from bots
        if (message.Author.IsBot)
        {
            return false;
        }

        // Ignore message if it doesn't start with the command prefix
        if (!IsCommandMessage(message))
        {
            return false;
        }

        // Try each command until one handles the message
        foreach (var command in _commands)
        {
            try
            {
                if (await command.HandleAsync(message))
                {
                    _logger.LogDebug("Command {CommandName} handled message from {Username}", 
                        command.CommandName, 
                        message.Author.Username);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command {CommandName}", command.CommandName);
            }
        }

        // If no command handled the message, reply with help
        await ReplyWithHelpAsync(message);
        
        return false;
    }

    private static bool IsCommandMessage(SocketMessage message)
    {
        return message.Content.StartsWith('!');
    }

    private async Task ReplyWithHelpAsync(SocketMessage message)
    {
        try
        {
            var helpOutput = HelpCommand.BuildHelpMessage(_commands);
            await message.Channel.SendMessageAsync(helpOutput);
        }
        catch (Exception error)
        {
            _logger.LogError(error, "❌ Error replying with help for unknown command");
        }
    }

    /// <summary>
    /// Gets a command by its name
    /// </summary>
    public T? GetCommand<T>() where T : ICommand
    {
        return _commands.OfType<T>().FirstOrDefault();
    }
}
