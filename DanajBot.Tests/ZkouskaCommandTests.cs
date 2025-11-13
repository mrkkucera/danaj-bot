using AwesomeAssertions;
using DanajBot.Commands.Zkouska;
using NUnit.Framework;

namespace DanajBot.Tests;

[TestFixture]
public class ZkouskaCommandTests
{

  [Test]
  [TestCase("a", "a #12345678")]
  [TestCase("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890", "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890 #12345678")]
  [TestCase("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890abcdefghijklmonp", "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890 #12345678")]
  public void CreateThreadName_NameIsTruncatedIfTooLong(string description, string expectedThreadName)
  {
    
    var threadName = ZkouskaMessageBuilder.CreateThreadName(description, "12345678");
    threadName.Length.Should()
      .BeLessThanOrEqualTo(100, "because Discord limits the name of a thread to 100 characters");
    threadName.Should().Be(expectedThreadName);
  }
}
