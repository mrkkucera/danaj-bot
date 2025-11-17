namespace DanajBot.Settings;

internal class EveryonePermissionChecksSettings
{
  public ulong VerificationCategoryId { get; set; }
  public int PermissionCheckIntervalMinutes { get; set; } = 60;
}