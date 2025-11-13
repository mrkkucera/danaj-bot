using DanajBot.Commands.Zkouska;
using System.Text.RegularExpressions;

namespace DanajBot.Commands;

/// <summary>
/// Utility methods for zkouska operations
/// </summary>
internal static partial class ZkouskaHelper
{
    /// <summary>
    /// Regex for extracting zkouska ID from message content
    /// </summary>
    [GeneratedRegex(ZkouskaConstants.ZkouskaIdPattern)]
    internal static partial Regex ZkouskaIdRegex();

    /// <summary>
    /// Extracts zkouska ID from message content
    /// </summary>
    public static string? ExtractZkouskaId(string content)
    {
        var match = ZkouskaIdRegex().Match(content);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Checks if a message is a zkouska message
    /// </summary>
    public static bool IsZkouskaMessage(string content)
    {
        return content.StartsWith(ZkouskaConstants.ZkouskaMessagePrefix);
    }

    /// <summary>
    /// Extracts user ID from embed footer text
    /// </summary>
    public static bool TryExtractUserIdFromFooter(string? footerText, out ulong userId)
    {
        userId = 0;
        
        if (footerText == null || !footerText.StartsWith(ZkouskaConstants.UserIdPrefix))
            return false;
        
        var userIdStr = footerText.Substring(ZkouskaConstants.UserIdPrefix.Length);
        return ulong.TryParse(userIdStr, out userId);
    }

    /// <summary>
    /// Checks if an embed is a creator message (vs absence message)
    /// </summary>
    public static bool IsCreatorMessage(string? footerText)
    {
        return footerText != null && footerText.Contains(ZkouskaConstants.ZkouskaIdFooterPrefix);
    }

    /// <summary>
    /// Gets display name for a user
    /// </summary>
    public static string GetDisplayName(string? displayName, string username)
    {
        return displayName ?? username;
    }

    /// <summary>
    /// Gets avatar URL for a user
    /// </summary>
    public static string GetAvatarUrl(string? avatarUrl, string defaultAvatarUrl)
    {
        return avatarUrl ?? defaultAvatarUrl;
    }
}
