namespace DanajBot.Commands.Zkouska;

/// <summary>
/// Constants used by the Zkouska command system
/// </summary>
internal static class ZkouskaConstants
{
    // Emojis
    public const string AbsenceEmoji = "❌";
    public const string LateEmoji = "🕐";
    public const string AttendingEmoji = "✅";
    public const string CloseEmoji = "🛑";
    public const string ZkouskaEmoji = "📝";
    public const string LockEmoji = "🔒";
    
    // Message templates
    public const string NoPermissionMessage = "🔒 Potřebujete moderátorské oprávnění k vytvoření zkoušky!";
    public const string MissingDescriptionMessage = "⚠️ Chybí popis! Použití: `{0} <description>`";
    public const string LogChannelNotFoundMessage = "❌ Chyba: Nelze najít kanál pro omluvenky.";
    public const string AnnouncementChannelNotFoundMessage = "❌ Chyba: Nelze najít kanál pro zkoušky.";
    public const string CreateErrorMessage = "❌ Chyba pri vytvareni zkousky. Zkuste to prosím znovu.";
    
    // Regex patterns
    public const string ZkouskaIdPattern = @"`(#[a-fA-F0-9]{8})`";
    public const string ZkouskaMessagePrefix = "📝 **Zkouška**";
    
    // Embed footer prefixes
    public const string UserIdPrefix = "User ID: ";
    public const string ZkouskaIdFooterPrefix = "Zkouška ID:";
    
    // Configuration
    public const int ZkouskaIdLength = 8;
    public const int MaxThreadNameLength = 100;
}
