# DanajBot - Discord Zkouška Manager

A Discord bot that manages zkouška (practice) announcements and tracks absences through reactions.

## Features

### Zkouška Management
- 📝 Create zkouška announcements with `!zkouska <description>` command
- ❌ Users can react with ❌ to excuse themselves from a zkouška
- 🧵 Automatically creates threads for each zkouška in a destination channel
- 👤 Tracks and logs all absences with user information and timestamps
- 🔄 Prevents duplicate reactions from the same user
- 💾 Rebuilds state from Discord on startup (persistent across restarts)
- 🗑️ Automatically deletes command messages to keep channels clean

### Bot Management
- 💬 All bot commands only work in the designated bot chat channel
- 💬 `!help` command shows available commands and their usage 
- 🏥 Health check HTTP endpoint for monitoring bot status
- 🔐 Automatic monitoring of @everyone role permissions across all categories and channels
- ⚠️ Reports permission issues to a designated bot chat channel
- ⏰ Configurable permission check intervals

## Prerequisites

- .NET 10.0 SDK or higher
- A Discord account
- A Discord server where you have admin permissions

## Health Check Endpoint

The bot exposes a health check HTTP server on port 8080 for monitoring and uptime checks:

### Endpoints

- **GET /health** - Returns bot connection status in JSON format
  ```json
  {
    "status": "Healthy",
    "checks": [
      {
        "name": "discord_bot",
        "status": "Healthy",
        "description": "Bot is connected to Discord",
        "duration": 0.5
      }
    ],
    "totalDuration": 0.5
  }
  ```
  - Status codes: `200 OK` when healthy, `503 Service Unavailable` when unhealthy
  
- **GET /** - Simple root endpoint that returns bot status and timestamp

### Using with Digital Ocean

When deploying to Digital Ocean, configure health checks:
- **HTTP Path**: `/health`
- **Port**: `8080`
- **Success Status Code**: `200`

The health check monitors the Discord connection status and will report unhealthy if the bot is not connected to Discord.

## Setup Instructions

### 1. Create a Discord Bot

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications)
2. Click "New Application" and give it a name
3. Go to the "Bot" section
4. Click "Add Bot"
5. Under "Privileged Gateway Intents", enable:
   - **MESSAGE CONTENT INTENT** (required to read message content)
   - Server Members Intent (optional)
   - Presence Intent (optional)
6. Click "Reset Token" and copy your bot token (save it securely!)

### 2. Invite the Bot to Your Server

1. In the Developer Portal, go to "OAuth2" > "URL Generator"
2. Select the following scopes:
   - `bot`
3. Select the following bot permissions:
   - Read Messages/View Channels
   - Send Messages
   - Manage Messages (to delete messages)
   - Embed Links
   - Attach Files
   - Read Message History
4. Copy the generated URL and open it in your browser
5. Select your server and authorize the bot

### 3. Get Channel IDs

1. Enable Developer Mode in Discord:
   - User Settings > App Settings > Advanced > Developer Mode
2. Right-click on the source channel (to monitor) and click "Copy Channel ID"
3. Right-click on the destination channel (to forward to) and click "Copy Channel ID"
4. (Optional) Right-click on the channel to monitor permissions and click "Copy Channel ID"

### 4. Configure the Bot

1. Clone or download this repository
2. Create an `appsettings.json` file in the `DanajBot` directory or set environment variables:
   ```json
   {
     "AppSettings": {
       "DiscordToken": "your_bot_token_here",
       "BotChatChannelId": 123456789012345678,
       "Zkouska": {
         "SourceChannelId": 123456789012345678,
         "DestinationChannelId": 123456789012345678
       },
       "EveryonePermissionChecksSettings": {
         "VerificationCategoryId": 123456789012345678,
         "PermissionCheckIntervalMinutes": 60
       }
     }
   }
   ```

   **Environment Variable Format** (for Docker/hosting):
   ```bash
   AppSettings__DiscordToken=your_bot_token_here
   AppSettings__BotChatChannelId=123456789012345678
   AppSettings__Zkouska__SourceChannelId=123456789012345678
   AppSettings__Zkouska__DestinationChannelId=123456789012345678
   AppSettings__EveryonePermissionChecksSettings__VerificationCategoryId=123456789012345678
   AppSettings__EveryonePermissionChecksSettings__PermissionCheckIntervalMinutes=60
   ```

   **Configuration Options:**
   - `DiscordToken` (required): Your bot's authentication token from Discord Developer Portal
   - `BotChatChannelId` (required): Channel ID where:
     - Permission issues are reported
     - All bot commands are accepted
   - `Zkouska.SourceChannelId` (required): Channel ID where zkouška announcements are posted
   - `Zkouska.DestinationChannelId` (required): Channel ID where absence threads are created
   - `EveryonePermissionChecksSettings.VerificationCategoryId` (optional): Category ID for verification channels to monitor (set to 0 to disable permission monitoring)
   - `EveryonePermissionChecksSettings.PermissionCheckIntervalMinutes` (optional): How often to check permissions in minutes (default: 60)

### 5. Run the Bot

#### Using .NET CLI:
```bash
cd DanajBot
dotnet restore
dotnet run
```

#### Using pre-built Docker image:
```bash
docker pull ghcr.io/mrkkucera/danaj-bot:latest

# Basic run
docker run -p 8080:8080 \
  -e AppSettings__DiscordToken=your_token \
  -e AppSettings__BotChatChannelId=123456789012345678 \
  -e AppSettings__Zkouska__SourceChannelId=123456789012345678 \
  -e AppSettings__Zkouska__DestinationChannelId=123456789012345678 \
  ghcr.io/mrkkucera/danaj-bot:latest

# With permission monitoring
docker run -p 8080:8080 \
  -e AppSettings__DiscordToken=your_token \
  -e AppSettings__BotChatChannelId=123456789012345678 \
  -e AppSettings__Zkouska__SourceChannelId=123456789012345678 \
  -e AppSettings__Zkouska__DestinationChannelId=123456789012345678 \
  -e AppSettings__EveryonePermissionChecksSettings__VerificationCategoryId=123456789012345678 \
  ghcr.io/mrkkucera/danaj-bot:latest
```

**Note**: Make sure to expose port 8080 when running in Docker to access the health endpoint.

You should see a message indicating the bot is online and monitoring the specified channel.

## Configuration

Configuration can be done through `appsettings.json` or environment variables (using double underscore `__` as separator):

### Required Settings
- `DiscordToken`: Your bot's authentication token
- `BotChatChannelId`: Channel ID for bot commands and notifications
- `Zkouska.SourceChannelId`: Channel ID where zkouška announcements are posted
- `Zkouska.DestinationChannelId`: Channel ID where absence threads are created

### Optional Settings
- `EveryonePermissionChecksSettings.VerificationCategoryId`: Category ID to monitor for permissions (set to `0` to disable monitoring)
- `EveryonePermissionChecksSettings.PermissionCheckIntervalMinutes`: How often to check permissions (default: 60 minutes)

## How It Works

### Zkouška Workflow
1. **Creating a Zkouška**: Moderators use `!zkouska <description>` in the bot chat channel
   - Bot posts an announcement with ❌ reaction in the source channel
   - Creates a thread in the destination channel with the zkouška details
   - Deletes the command message to keep the channel clean
   
2. **Excusing from Zkouška**: Users react with ❌ to the announcement
   - Bot logs the absence in the corresponding thread with user mention and timestamp
   - Removes the user's reaction (keeps UI clean)
   - Prevents duplicate reactions from the same user
   
3. **State Persistence**: On startup, the bot:
   - Scans through message history in the source channel
   - Rebuilds all zkouška announcements and their associated threads
   - Restores tracking for all existing announcements

### Permission Monitoring
The bot automatically monitors permissions every hour (configurable):
- **Verification Category**: Validates that @everyone role has required permissions:
  - ViewChannel
  - ReadMessageHistory
  - SendMessages
  - AddReactions
  - MentionEveryone
- **Other Categories**: Ensures @everyone role has ViewChannel permission denied
- **Reporting**: All permission issues are logged and reported to the bot chat channel

### Help System
- All bot commands must be typed in the designated bot chat channel
- Users can type `!help` to see:
  - All available commands
  - Command syntax and descriptions
  - Usage examples

## Required Bot Permissions

The bot requires the following permissions to function properly:

### Basic Permissions
- **Read Messages/View Channels**: To see messages in both source and destination channels
- **Send Messages**: To post announcements and thread messages
- **Manage Messages**: To delete command messages and remove reactions
- **Embed Links**: To send rich embed messages in threads
- **Read Message History**: To rebuild state on startup

### Thread Permissions
- **Create Public Threads**: To create absence tracking threads
- **Send Messages in Threads**: To post absence logs in threads

### Reaction Permissions
- **Add Reactions**: To add the initial ❌ reaction to announcements
- **Manage Messages**: To remove user reactions after logging

### Permission Monitoring (if enabled)
- **View Channels**: To access all categories and channels for permission checks

## Technology Stack

- **.NET 10.0**: Latest version of .NET platform
- **C# 14.0**: Latest C# language features
- **Discord.Net**: Discord API wrapper for .NET
- **ASP.NET Core**: For health check HTTP endpoint
- **Microsoft.Extensions.Hosting**: For background service hosting

## Project Structure

```
DanajBot/
├── Commands/           # Command implementations
│   ├── Zkouska/       # Zkouska-specific command logic
│   ├── HelpCommand.cs # Help command implementation
│   └── ICommand.cs    # Command interface
├── Services/          # Background services
│   ├── BotService.cs              # Main Discord bot service
│   ├── BotHostedService.cs        # Bot lifecycle management
│   └── PermissionCheckerService.cs # Permission monitoring
├── Settings/          # Configuration models
└── Program.cs         # Application entry point
```

## Development

### Building from Source

```bash
git clone https://github.com/mrkkucera/danaj-bot.git
cd danaj-bot
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Docker Build

```bash
docker build -t danajbot .
docker run -p 8080:8080 --env-file .env danajbot
```

## Commands

**Note**: All commands must be used in the bot chat channel (configured via `BotChatChannelId`).

- **`!zkouska <description>`** - Creates a new zkouška announcement with the given description (requires Manage Messages permission)
  - Example: `!zkouska Test run for Sunday concert`
  - The command message is automatically deleted after posting the announcement
  - Creates a thread in the destination channel for tracking absences
  
- **`!help`** - Shows all available commands and their usage (only works in bot chat channel)
  - Displays command syntax and descriptions
  - Automatically deletes the command message

## Troubleshooting

### Bot is not responding
- Check that the bot is online in your server (green dot next to bot name)
- Verify the MESSAGE CONTENT INTENT is enabled in the Developer Portal
- Ensure the bot has proper permissions in both channels
- Check bot logs for error messages

### Cannot create zkouška
- Verify you have "Manage Messages" permission in the source channel
- Check that the bot can create threads in the destination channel
- Ensure bot role has "Create Public Threads" permission

### Reactions not being tracked
- Ensure the bot has "Manage Messages" permission to remove reactions
- Check that the bot's role is higher than the user's role in the server hierarchy
- Verify the bot has "Read Message History" permission

### Commands not working
- Ensure you're typing commands in the bot chat channel (configured in `BotChatChannelId`)
- Check that the bot has permission to send messages and delete messages in that channel
- Verify the MESSAGE CONTENT INTENT is enabled in the Developer Portal

### Permission monitoring not working
- Verify `VerificationCategoryId` is set correctly (use Developer Mode to copy category ID)
- Check that the bot has permission to view all categories
- Ensure `PermissionCheckIntervalMinutes` is not set to 0
- Look for permission issue reports in the bot chat channel

### Health check returns unhealthy
- Verify the bot token is correct and valid
- Check that the bot has been invited to your Discord server
- Ensure network connectivity to Discord services (discord.com, gateway.discord.gg)
- Check bot logs for connection errors

### State not rebuilding after restart
- Bot needs "Read Message History" permission in source channel
- Ensure bot has access to view threads in destination channel
- Check bot logs for any errors during state rebuild

## License

ISC
