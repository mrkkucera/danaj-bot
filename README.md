# DanajBot - Discord Zkouška Manager

A Discord bot that manages zkouška (practice) announcements and tracks absences through reactions.

## Features

- 📝 Create zkouška announcements with `!zkouska <description>`
- ❌ Users can react with ❌ to excuse themselves from a zkouška
- 🧵 Automatically creates threads for each zkouška in a destination channel
- 👤 Tracks and logs all absences with user information
- 🔄 Prevents duplicate reactions from the same user
- 💾 Rebuilds state from Discord on startup (persistent across restarts)
- 🏥 Health check endpoint for monitoring bot status

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

### 4. Configure the Bot

1. Clone or download this repository
2. Create an `appsettings.json` file or set environment variables:
   ```json
   {
     "AppSettings__DiscordToken": "your_bot_token_here",
     "AppSettings__Zkouska__SourceChannelId": "your_source_channel_id_here",
     "AppSettings__Zkouska__DestinationChannelId": "your_destination_channel_id_here"
   }
   ```

### 5. Run the Bot

#### Using .NET CLI:
```bash
dotnet restore
dotnet run
```

#### Using pre-built Docker image:
```bash
docker pull ghcr.io/mrkkucera/danaj-bot:latest
docker run -p 8080:8080 -e AppSettings__DiscordToken=your_token -e AppSettings__Zkouska__SourceChannelId=id -e AppSettings__Zkouska__DestinationChannelId=id ghcr.io/mrkkucera/danaj-bot:latest
```

**Note**: Make sure to expose port 8080 when running in Docker to access the health endpoint.

You should see a message indicating the bot is online and monitoring the specified channel.

## Configuration

Configuration can be done through `appsettings.json` or environment variables:

- `DISCORD_TOKEN`: Your bot's authentication token
- `SOURCE_CHANNEL_ID`: The channel ID where zkouška announcements are posted
- `DESTINATION_CHANNEL_ID`: The channel ID where absence threads are created

## How It Works

1. **Creating a Zkouška**: Moderators use `!zkouska <description>` in the source channel
   - Bot posts an announcement with ❌ reaction
   - Creates a thread in the destination channel
   - Deletes the command message
2. **Excusing from Zkouška**: Users react with ❌ to the announcement
   - Bot logs the absence in the corresponding thread
   - Removes the user's reaction (keeps UI clean)
   - Prevents duplicate reactions
3. **State Persistence**: On startup, the bot rebuilds its state from Discord history

## Required Bot Permissions

- **Read Messages/View Channels**: To see messages in both channels
- **Send Messages**: To post announcements and thread messages
- **Manage Messages**: To delete command messages and remove reactions
- **Embed Links**: To send rich embed messages in threads
- **Create Public Threads**: To create absence tracking threads
- **Add Reactions**: To add the initial ❌ reaction to announcements

## Commands

- `!zkouska <description>` - Creates a new zkouška announcement (requires Manage Messages permission)

## Troubleshooting

### Bot is not responding
- Check that the bot is online in your server
- Verify the MESSAGE CONTENT INTENT is enabled in the Developer Portal
- Ensure the bot has proper permissions in both channels

### Cannot create zkouška
- Verify you have "Manage Messages" permission
- Check that the bot can create threads in the destination channel

### Reactions not being tracked
- Ensure the bot has "Manage Messages" permission to remove reactions
- Check that the bot's role is higher than the user's role in the server hierarchy

### Health check returns unhealthy
- Verify the bot token is correct and valid
- Check that the bot has been invited to your Discord server
- Ensure network connectivity to Discord services

## License

ISC
