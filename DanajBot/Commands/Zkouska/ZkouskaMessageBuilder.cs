using Discord;

namespace DanajBot.Commands.Zkouska;

/// <summary>
/// Builds formatted messages and embeds for zkouska operations
/// </summary>
internal static class ZkouskaMessageBuilder
{
    /// <summary>
    /// Creates the main zkouska announcement message
    /// </summary>
    public static string CreateZkouskaMessage(string zkouskaId, string description)
    {
        return $"{ZkouskaConstants.ZkouskaEmoji} **Zkouška** `#{zkouskaId}`\n\n" +
               $"{description}\n\n" +
               $"*Reagujte pomocí {ZkouskaConstants.AbsenceEmoji} pokud se chcete omluvit z této zkoušky. " +
               $"Vaše reakce po chvilce zmizí, ale bude zaznamenána.*\n" +
               $"*Moderátoři můžou reagovat pomocí {ZkouskaConstants.DeleteEmoji}, aby uzavřeli omluvenky na tuto zkoušku.*";
    }

    /// <summary>
    /// Creates an embed for zkouska creation notification in thread
    /// </summary>
    public static Embed CreateCreatorEmbed(string displayName, string avatarUrl, string description, string zkouskaId, ulong userId)
    {
        return new EmbedBuilder()
            .WithAuthor(displayName, avatarUrl)
            .WithDescription($"**Zkouška vytvořena:**\n{description}")
            .WithColor(Color.Green)
            .WithCurrentTimestamp()
            .WithFooter($"{ZkouskaConstants.ZkouskaIdFooterPrefix} {zkouskaId} | {ZkouskaConstants.UserIdPrefix}{userId}")
            .Build();
    }

    /// <summary>
    /// Creates an embed for absence notification in thread
    /// </summary>
    public static Embed CreateAbsenceEmbed(string displayName, string avatarUrl, ulong userId)
    {
        return new EmbedBuilder()
            .WithAuthor(displayName, avatarUrl)
            .WithDescription("**Omluvenka ze zkoušky**")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp()
            .WithFooter($"{ZkouskaConstants.UserIdPrefix}{userId}")
            .Build();
    }

    /// <summary>
    /// Creates an embed for zkouska closing notification
    /// </summary>
    public static Embed CreateClosingEmbed(string displayName, string avatarUrl)
    {
        return new EmbedBuilder()
            .WithAuthor(displayName, avatarUrl)
            .WithDescription($"{ZkouskaConstants.LockEmoji} **Příjem omluvenek uzavřen**\n\nTato zkouška byla uzavřena moderátorem.")
            .WithColor(Color.Red)
            .WithCurrentTimestamp()
            .Build();
    }

    /// <summary>
    /// Creates a thread name from description and zkouska ID
    /// </summary>
    public static string CreateThreadName(string description, string zkouskaId)
    {
      var zkouskaSuffix = $" #{zkouskaId}";
      if (description.Length + zkouskaSuffix.Length > ZkouskaConstants.MaxThreadNameLength)
      {
        description = description.AsSpan(0, ZkouskaConstants.MaxThreadNameLength - zkouskaSuffix.Length).ToString();
      }
      
      return $"{description} #{zkouskaId}";
    }

    /// <summary>
    /// Generates a unique zkouska ID
    /// </summary>
    public static string GenerateZkouskaId()
    {
        return Guid.NewGuid().ToString("N")[..ZkouskaConstants.ZkouskaIdLength];
    }
}
