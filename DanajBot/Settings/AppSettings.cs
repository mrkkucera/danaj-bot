namespace DanajBot.Settings;

internal class AppSettings
{
  public string DiscordToken { get; set; } = string.Empty;
  public ulong BotChatChannelId { get; set; }
  public ZkouskaSettings Zkouska { get; set; } = new();
  public EveryonePermissionChecksSettings EveryonePermissionChecksSettings { get; set; } = new();
}