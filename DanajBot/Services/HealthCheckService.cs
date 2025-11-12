using Discord.WebSocket;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DanajBot.Services;

/// <summary>
/// Health check service that monitors Discord bot connection status
/// </summary>
internal class HealthCheckService : IHealthCheck
{
    private readonly DiscordSocketClient _client;

    public HealthCheckService(DiscordSocketClient client)
    {
        _client = client;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Check if the bot is connected to Discord
        if (_client.ConnectionState == Discord.ConnectionState.Connected && _client.LoginState == Discord.LoginState.LoggedIn)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Bot is connected to Discord"));
        }

        var status = $"ConnectionState: {_client.ConnectionState}, LoginState: {_client.LoginState}";
        return Task.FromResult(HealthCheckResult.Unhealthy($"Bot is not connected. {status}"));
    }
}
