namespace DanajBot.Settings;

internal class AppSettings
{
  public string DiscordToken { get; set; } = string.Empty;
  public ZkouskaSettings Zkouska { get; set; } = new();
}