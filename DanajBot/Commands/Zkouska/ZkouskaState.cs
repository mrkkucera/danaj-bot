using System.Collections.Concurrent;

namespace DanajBot.Commands.Zkouska;

public class ZkouskaState
{
  /// <summary>
  /// Mapping of zkouska message IDs to their corresponding thread IDs
  /// </summary>
  public ConcurrentDictionary<ulong, ulong> ZkouskaMessageIdToThreadId { get; init; } = new();
  
  /// <summary>
  /// Mapping of zkouska message IDs to sets of user identifiers who have reacted to each message.
  /// </summary>
  public ConcurrentDictionary<ulong, HashSet<ulong>> ZkouskaMessageToUserReactions { get; init; } = new();
  
  /// <summary>
  /// Mapping of (zkouska message ID, user ID) to the thread message ID for their reaction notification
  /// </summary>
  public ConcurrentDictionary<(ulong MessageId, ulong UserId), ulong> UserReactionToThreadMessageId { get; init; } = new();
}
