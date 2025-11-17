using DanajBot.Settings;
using Discord;
using Discord.WebSocket;

namespace DanajBot.Services;

/// <summary>
/// Periodically checks if @everyone role has correct permissions in a specific channel
/// </summary>
internal class PermissionCheckerService : BackgroundService
{
  private readonly DiscordSocketClient _client;
  private readonly EveryonePermissionChecksSettings _everyonePermissionChecksSettings;
  private readonly AppSettings _settings;
  private readonly ILogger<PermissionCheckerService> _logger;
  private readonly PeriodicTimer _timer;

  public PermissionCheckerService(
    DiscordSocketClient client,
    EveryonePermissionChecksSettings everyonePermissionChecksSettings,
    AppSettings settings,
    ILogger<PermissionCheckerService> logger)
  {
    _client = client;
    _everyonePermissionChecksSettings = everyonePermissionChecksSettings;
    _settings = settings;
    _logger = logger;
    _timer = new PeriodicTimer(TimeSpan.FromMinutes(_everyonePermissionChecksSettings.PermissionCheckIntervalMinutes));
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (_everyonePermissionChecksSettings.VerificationCategoryId == 0)
    {
      _logger.LogInformation("⚠️ Permission check disabled - PermissionCheckChannelId not configured");
      return;
    }

    _logger.LogInformation("🔐 Starting permission checker service for channel {ChannelId}",
      _everyonePermissionChecksSettings.VerificationCategoryId);
    _logger.LogInformation("🕐 Check interval: {IntervalMinutes} minutes", _everyonePermissionChecksSettings.PermissionCheckIntervalMinutes);


    // run once after 10 seconds delay to allow bot to connect
    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
    await CheckEveryoneRolePermissionsOnServer();

    try
    {
      while (await _timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
      {
        await CheckEveryoneRolePermissionsOnServer();
      }
    }
    finally
    {
      _logger.LogInformation("🔐 Stopping permission checker service");
    }
  }

  private async Task CheckEveryoneRolePermissionsOnServer()
  {
    _logger.LogInformation("🔐 Checking @everyone role permissions on server");
    try
    {
      if (_client.ConnectionState != ConnectionState.Connected)
      {
        _logger.LogWarning("⚠️ Bot not connected, skipping permission check");
        return;
      }

      var category = await _client.GetChannelAsync(_everyonePermissionChecksSettings.VerificationCategoryId);

      if (category is not SocketCategoryChannel verificationCategory)
      {
        _logger.LogError("❌ Channel {ChannelId} not found or is not a category channel", _everyonePermissionChecksSettings.VerificationCategoryId);
        return;
      }

      var guild = verificationCategory.Guild;
      var everyoneRole = guild.EveryoneRole;

      var issues = new List<string>(0);

      CheckGuildLevelPermissions(everyoneRole, issues);
      CheckVerificationCategory(verificationCategory, everyoneRole, issues);
      CheckOtherCategories(guild, verificationCategory, everyoneRole, issues);

      await RespondWithIssues(issues);
    }
    catch (Exception error)
    {
      _logger.LogError(error, "❌ Error checking permissions");
    }
  }

  private static void CheckGuildLevelPermissions(SocketRole everyoneRole, List<string> issues)
  {
    var guildPermissions = everyoneRole.Permissions.ToList();
    // clyde AI has been discontinued, request to speak is in development
    var nonAllowedPermissions = guildPermissions
      .Except([GuildPermission.UseClydeAI, GuildPermission.RequestToSpeak, GuildPermission.ChangeNickname])
      .ToList();
    
    if (nonAllowedPermissions.Count > 0)
    {
      issues.Add("Role `everyone` má na celém serveru povolené některá oprávnění. `everyone` má být přepsán v kanálech, kde by měli mít neověření členové povolenou akci.");
      issues.Add($"Povolená oprávnění `everyone` na celém serveru jsou: {string.Join(", ", nonAllowedPermissions)}");
    }
  }

  private static void CheckVerificationCategory(SocketCategoryChannel verificationCategory, SocketRole everyoneRole, List<string> issues)
  {
    var categoryPermissionOverwrite = verificationCategory.GetPermissionOverwrite(everyoneRole);

    if (categoryPermissionOverwrite is null)
    {
      issues.Add($"⚠️ Kategorie {verificationCategory.Name} nemá přepsané práva pro roli `everyone`. Noví uživatelé se tak nemůžou ověřit, protože tuto kategorii neuvidí.");
      return;
    }

    CheckVerificationChannelRequiredPermissions(categoryPermissionOverwrite.Value, verificationCategory, issues);
    CheckVerificationCategoryChildChannels(verificationCategory, everyoneRole, issues);
  }

  private static void CheckVerificationChannelRequiredPermissions(
    OverwritePermissions permissions,
    SocketGuildChannel channel,
    List<string> issues)
  {
    var categoryIssues = new List<string>(0);

    if (permissions.ReadMessageHistory != PermValue.Allow)
    {
      categoryIssues.Add($"❌ ReadMessageHistory není povolena (aktuální nastavení: {permissions.ReadMessageHistory})");
    }

    if (permissions.ViewChannel != PermValue.Allow)
    {
      categoryIssues.Add($"❌ ViewChannel není povolena (aktuální nastavení: {permissions.ViewChannel})");
    }

    if (permissions.AddReactions != PermValue.Allow)
    {
      categoryIssues.Add($"❌ AddReactions není povolena (aktuální nastavení: {permissions.AddReactions})");
    }

    if (permissions.SendMessages != PermValue.Allow)
    {
      categoryIssues.Add($"❌ SendMessages není povolena (aktuální nastavení: {permissions.SendMessages})");
    }

    if (permissions.MentionEveryone != PermValue.Allow)
    {
      categoryIssues.Add($"❌ MentionEveryone není povolena (aktuální nastavení: {permissions.MentionEveryone})");
    }

    CheckUnexpectedAllowedPermissions(permissions, categoryIssues);

    if (categoryIssues.Count > 0)
    {
      issues.Add($"Problémy s právy v kanále/kategorii {channel.Name}({channel.Id}):");
      issues.AddRange(categoryIssues);
    }
  }

  private static void CheckVerificationCategoryChildChannels(
    SocketCategoryChannel verificationCategory,
    SocketRole everyoneRole,
    List<string> issues)
  {
    foreach (var channel in verificationCategory.Channels)
    {
      if (channel is SocketThreadChannel)
      {
        continue;
      }

      var channelPermissionOverwrite = channel.GetPermissionOverwrite(everyoneRole);

      if (channelPermissionOverwrite is not null)
      {

        CheckVerificationChannelRequiredPermissions(channelPermissionOverwrite.Value, channel, issues);
      }
    }
  }

  private static void CheckOtherCategories(
    SocketGuild guild,
    SocketCategoryChannel verificationCategory,
    SocketRole everyoneRole,
    List<string> issues)
  {
    var categories = guild.CategoryChannels.Where(c => c.Id != verificationCategory.Id);

    foreach (var category in categories)
    {
      var categoryPermissionOverwrite = category.GetPermissionOverwrite(everyoneRole);

      if (categoryPermissionOverwrite is null)
      {
        issues.Add($"⚠️ Kategorie {category.Name} ({category.Id}) nemá přepsané práva pro `everyone`. Kategorie by měla mít explicitně zakázáno oprávnění ViewChannel.");
      }
      else if (categoryPermissionOverwrite.Value.ViewChannel != PermValue.Deny)
      {
        issues.Add($"⚠️ Kategorie {category.Name} ({category.Id}) nemá zakázaný ViewChannel pro `everyone` (aktuální nastavení: {categoryPermissionOverwrite.Value.ViewChannel}).");
      }

      CheckOtherCategoryChildChannels(category, everyoneRole, issues);
    }
  }

  private static void CheckOtherCategoryChildChannels(
    SocketCategoryChannel category,
    SocketRole everyoneRole,
    List<string> issues)
  {
    foreach (var channel in category.Channels)
    {
      if (channel is SocketThreadChannel)
      {
        continue;
      }

      var channelPermissionOverwrite = channel.GetPermissionOverwrite(everyoneRole);
      
      if (channelPermissionOverwrite is not null && channelPermissionOverwrite.Value.ViewChannel != PermValue.Deny)
      {
        issues.Add($"⚠️ Kanál {channel.Name} ({channel.Id}) v kategorii {category.Name} nemá explicitně zakázáno oprávněni ViewChannel pro `everyone`.");
      }
    }
  }
  
  private static void CheckUnexpectedAllowedPermissions(OverwritePermissions permissions, List<string> issues)
  {
    var allowedPermissions = ChannelPermission.ReadMessageHistory | 
                           ChannelPermission.AddReactions | 
                           ChannelPermission.SendMessages | 
                           ChannelPermission.MentionEveryone |
                           ChannelPermission.ViewChannel;

    var unexpectedAllowed = permissions.AllowValue & ~(ulong)allowedPermissions;
    
    if (unexpectedAllowed != 0)
    {
      var unexpectedPermissions = new ChannelPermissions(unexpectedAllowed);
      var permissionNames = unexpectedPermissions.ToList();
      
      foreach (var permName in permissionNames)
      {
        issues.Add($"❌ {permName} by mělo být zakázáno, ale je povoleno");
      }
    }
  }

  private async Task RespondWithIssues(List<string> issues)
  {
    if (issues.Count == 0)
    {
      return;
    }

    _logger.LogWarning("⚠️ Permission issues detected:");
    foreach (var issue in issues)
    {
      _logger.LogWarning("  {Issue}", issue);
    }

    await SendIssuesToDiscordAsync(issues);
  }

  private async Task SendIssuesToDiscordAsync(List<string> issues)
  {
    if (_settings.BotChatChannelId == 0)
    {
      _logger.LogWarning("⚠️ BotChatChannelId not configured, skipping Discord notification");
      return;
    }

    try
    {
      var channel = await _client.GetChannelAsync(_settings.BotChatChannelId);

      if (channel is not IMessageChannel messageChannel)
      {
        _logger.LogError("❌ Bot chat channel {ChannelId} not found or is not a message channel", _settings.BotChatChannelId);
        return;
      }

      var message = BuildIssuesMessage(issues);
      await messageChannel.SendMessageAsync(message);
      
    }
    catch (Exception error)
    {
      _logger.LogError(error, "❌ Error sending permission issues to Discord");
    }
  }

  private static string BuildIssuesMessage(List<string> issues)
  {
    var messageBuilder = new System.Text.StringBuilder();
    messageBuilder.AppendLine($"🔐 **Problémy s oprávněními role `everyone`**");
    messageBuilder.AppendLine();
    
    foreach (var issue in issues)
    {
      messageBuilder.AppendLine(issue);
    }

    var message = messageBuilder.ToString();
    
    if (message.Length > 2000)
    {
      return message.Substring(0, 1997) + "...";
    }

    return message;
  }
}