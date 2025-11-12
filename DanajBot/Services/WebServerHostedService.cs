using Discord.WebSocket;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

namespace DanajBot.Services;

internal class WebServerHostedService : IHostedService
{
  private readonly ILogger<WebServerHostedService> _logger;
  private readonly DiscordSocketClient _discordClient;
  private WebApplication? _app;

  public WebServerHostedService(ILogger<WebServerHostedService> logger, DiscordSocketClient discordClient)
  {
    _logger = logger;
    _discordClient = discordClient;
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    try
    {
      var builder = WebApplication.CreateSlimBuilder();
      builder.WebHost.UseUrls("http://*:8080");

      // Register the Discord client from the main app in this WebApplication's service collection
      builder.Services.AddSingleton(_discordClient);
      
      // Register health checks with the Discord bot health check
      builder.Services.AddHealthChecks()
        .AddCheck<HealthCheckService>("discord_bot");

      _app = builder.Build();

      _app.MapHealthChecks("/health", new HealthCheckOptions
      {
        ResponseWriter = async (context, report) =>
        {
          context.Response.ContentType = "application/json";

          var response = new
          {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
              name = entry.Key,
              status = entry.Value.Status.ToString(),
              description = entry.Value.Description,
              duration = entry.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
          };

          await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
              WriteIndented = true
            }));
        }
      });

      _app.MapGet("/", () => Results.Ok(new { message = "DanajBot is running", timestamp = DateTime.UtcNow }));

      _logger.LogInformation("Starting health check HTTP server on port 8080");
      await _app.StartAsync(cancellationToken);
      _logger.LogInformation("Health check HTTP server started successfully. Endpoints: GET /health, GET /");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to start health check HTTP server");
      throw new InvalidOperationException("Failed to start health check HTTP server", ex);
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    if (_app != null)
    {
      _logger.LogInformation("Stopping health check HTTP server...");
      await _app.StopAsync(cancellationToken);
      await _app.DisposeAsync();
      _logger.LogInformation("Health check HTTP server stopped");
    }
  }
}
