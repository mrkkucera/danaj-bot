# DanajBot - Discord Message Forwarder

A Discord bot that monitors a specific channel, forwards messages to another channel, and deletes them from the original channel.

## Features

- 👀 Monitors a single designated channel
- 📤 Forwards messages to a destination channel with rich embeds
- 🗑️ Automatically deletes messages from the source channel
- 👤 Preserves author information and timestamps
- 📎 Includes attachment URLs in forwarded messages
- 🤖 Ignores messages from bots

## Prerequisites

- Node.js (v16.9.0 or higher)
- A Discord account
- A Discord server where you have admin permissions

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
2. Install dependencies:
   ```bash
   npm install
   ```
3. Edit the `.env` file and add your credentials:
   ```
   DISCORD_TOKEN=your_bot_token_here
   SOURCE_CHANNEL_ID=your_source_channel_id_here
   DESTINATION_CHANNEL_ID=your_destination_channel_id_here
   ```

### 5. Run the Bot

```bash
npm start
```

You should see a message indicating the bot is online and monitoring the specified channel.

## Configuration

All configuration is done through the `.env` file:

- `DISCORD_TOKEN`: Your bot's authentication token
- `SOURCE_CHANNEL_ID`: The channel ID to monitor (messages will be deleted from here)
- `DESTINATION_CHANNEL_ID`: The channel ID to forward messages to

## How It Works

1. The bot listens for messages in the source channel
2. When a user (not a bot) sends a message:
   - The bot creates an embed with the message content and author information
   - Sends the embed to the destination channel
   - Deletes the original message from the source channel
3. All operations are logged to the console

## Required Bot Permissions

- **Read Messages/View Channels**: To see messages in the source channel
- **Send Messages**: To send forwarded messages to the destination channel
- **Manage Messages**: To delete messages from the source channel
- **Embed Links**: To send rich embed messages

## Troubleshooting

### Bot is not responding
- Check that the bot is online in your server
- Verify the MESSAGE CONTENT INTENT is enabled in the Developer Portal
- Ensure the bot has proper permissions in both channels

### Messages are not being deleted
- Verify the bot has "Manage Messages" permission in the source channel
- Check that the bot's role is higher than the user's role in the server hierarchy

### Cannot forward messages
- Ensure the bot has "Send Messages" and "Embed Links" permissions in the destination channel

## License

ISC
