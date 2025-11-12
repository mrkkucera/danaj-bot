using Discord.WebSocket;

namespace DanajBot.Commands;

/// <summary>
/// Interface for Discord bot commands
/// </summary>
internal interface ICommand
{
    /// <summary>
    /// The command trigger (e.g., "!zkouska")
    /// </summary>
    string CommandName { get; }
    
    /// <summary>
    /// Handles the command execution
    /// </summary>
    /// <param name="message">The message that triggered the command</param>
    /// <returns>True if the command was handled, false otherwise</returns>
    Task<bool> HandleAsync(SocketMessage message);
}
