# GitHub Copilot Instructions for DanajBot

## Project Overview
This is a Discord bot project targeting .NET 10 with C# 14.0. The bot manages Discord server permissions, verification workflows, and custom commands.

## General Code Style

### Naming Conventions
- **Classes, Methods, Properties**: PascalCase
- **Private fields**: `_camelCase` with underscore prefix
- **Local variables**: camelCase
- **Constants**: PascalCase

### File Organization
- Use **file-scoped namespaces**: `namespace DanajBot.Services;`
- **Access modifiers**: Make everything `internal` unless classes need to be `public`
- **Member ordering**:
  1. Fields
  2. Constructor
  3. Public methods
  4. Private methods (placed after the public method that uses them, unless used in multiple places - then place after last usage)

### Formatting
- Always use **braces for control statements**, even for single-line blocks
- Use **spaces around operators**: `var x = 5` not `var x=5`
- Place opening braces on **new lines**
- Use **var** for local variables instead of explicit types

## Language Features & Patterns

### Null Handling
- Prefer `is null` over `== null`
- Use nullable reference types throughout
- Prefer null-coalescing operators (`??`, `??=`) when appropriate
- Use pattern matching for null checks

### Collections
- Prefer **collection expressions**: `[]` over `new List<>()` or `new[]`
- Use `.Any()` instead of `.Count > 0` for checking empty collections
- Prefer **LINQ** over traditional loops

### LINQ
- Format long LINQ chains with **one operation per line** with proper indentation:
```csharp
var result = collection
  .Where(x => x.IsValid)
  .Select(x => x.Name)
  .OrderBy(x => x);
```

### Async/Await
- Always use `Async` suffix for async methods
- No need to use `ConfigureAwait(false)` in this project

### String Operations
- Prefer **string interpolation**: `$"{variable}"` over `string.Format()` or concatenation
- Use `StringBuilder` only when concatenating many substrings (performance-sensitive scenarios)

### Expression-Bodied Members
- Use `=>` syntax for **one-liner methods and properties**
- Use full method bodies for multi-line logic

## Documentation & Comments

### XML Documentation
- Add **XML documentation comments** (`///`) for all **public** members
- Skip XML docs for internal/private members unless complex
- Keep documentation concise and technical

### Inline Comments
- **Do not** use inline comments for things that are obvious from variable names or control statements
- Use comments only to explain **why**, not **what**
- Example of good comment: `// clyde AI has been discontinued, request to speak is in development`
- **Never leave commented-out code** - delete it or implement it

## Architecture & Design

### Dependency Injection
- Use **constructor injection** for all dependencies
- Choose service lifetimes as needed (Singleton, Scoped, Transient)
- Only avoid DI when absolutely necessary

### Settings Management
- All configuration should come through **strongly-typed settings classes**
- Settings classes should use `required` properties or have default values
- Example: `EveryonePermissionCheckSettings`

### Immutability
- Prefer **immutable objects** where possible
- Use `readonly` for fields that don't change after construction
- Use `required` for mandatory initialization properties

### Error Handling
- Services should be **resilient** and continue running after errors
- Use structured try-catch blocks with appropriate logging
- Avoid fail-fast unless critical
- Example:
```csharp
try
{
  // operation
}
catch (Exception error)
{
  _logger.LogError(error, "❌ Error message");
}
```

### Disposal
- Implement `IDisposable` explicitly for classes with disposable **fields**
- Use `using` statements for local disposable resources

## Logging

### Structured Logging
- Use **emojis** in log messages for visual categorization:
  - 🔐 Security/permissions
  - ⚠️ Warnings
  - ❌ Errors
  - ✅ Success
  - 🕐 Timing/scheduling
  - 📝 General info
  
### Log Message Style
- Keep messages **technical** but not overly detailed
- Include relevant context (channel names/IDs, user info, etc.)
- Use structured logging parameters: `_logger.LogInformation("Message {Parameter}", value)`

Example:
```csharp
_logger.LogInformation("🔐 Starting permission checker service for channel {ChannelId}", channelId);
_logger.LogWarning("⚠️ Bot not connected, skipping permission check");
_logger.LogError(error, "❌ Error checking permissions");
```

## Configuration & Magic Numbers

### When to Make Values Configurable
- Make values configurable when they might vary across different use cases
- Example: Channel IDs that vary per Discord server
- **Magic numbers are acceptable** for:
  - Timing constants that rarely change
  - Hard-coded timeouts
  - Internal thresholds

### Feature Control
- Use **setting values** to enable/disable features (e.g., `VerificationChannelId == 0` to disable)
- No need for explicit feature flags

## Testing

### Test Naming
- Use format: `MethodName_Scenario_ExpectedBehavior`
- Example: `ProcessMessage_WhenUserIsNew_ShouldCreateUserRecord`

### Test Structure
- Use **Given/When/Then** pattern (Arrange/Act/Assert)
```csharp
// Given
var service = new MyService();

// When
var result = service.DoSomething();

// Then
Assert.Equal(expected, result);
```

## Code Quality

### Analyzer Rules
- **Do not suppress** analyzer warnings
- Fix warnings rather than suppressing them
- If a rule must be suppressed, discuss with the team first

### Best Practices
- Keep methods focused and single-purpose
- Extract complex logic into well-named private methods
- Prefer composition over inheritance
- Write self-documenting code through clear naming

## .NET 10 & C# 14 Features

### Leverage Modern Features
- Collection expressions: `[]`
- Do not use primary constructors 
- File-scoped namespaces
- Do not use global using directives 
- Required members
- Init-only properties where immutability is desired

## Example Code Structure

```csharp
namespace DanajBot.Services;

/// <summary>
/// Manages user verification workflows
/// </summary>
internal class VerificationService : IDisposable
{
  private readonly ILogger<VerificationService> _logger;
  private readonly VerificationSettings _settings;
  private Timer? _timer;

  public VerificationService(
    ILogger<VerificationService> logger,
    VerificationSettings settings)
  {
    _logger = logger;
    _settings = settings;
  }

  public async Task VerifyUserAsync(ulong userId)
  {
    if (userId == 0)
    {
      _logger.LogWarning("⚠️ Invalid user ID provided");
      return;
    }

    try
    {
      var user = await GetUserAsync(userId);
      
      if (user is null)
      {
        _logger.LogError("❌ User {UserId} not found", userId);
        return;
      }

      await ProcessVerificationAsync(user);
      _logger.LogInformation("✅ User {UserId} verified successfully", userId);
    }
    catch (Exception error)
    {
      _logger.LogError(error, "❌ Error verifying user {UserId}", userId);
    }
  }

  private async Task ProcessVerificationAsync(User user)
  {
    // Implementation
  }

  public void Dispose()
  {
    _timer?.Dispose();
  }
}
```

## Summary

Write clean, maintainable, and resilient code that follows modern C# practices. Prioritize readability through clear naming over excessive commenting. Use the type system and language features to make invalid states unrepresentable. Keep services focused and testable.
