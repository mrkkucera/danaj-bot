require('dotenv').config();
const { Client, GatewayIntentBits, EmbedBuilder } = require('discord.js');

// Create a new Discord client with necessary intents
const client = new Client({
    intents: [
        GatewayIntentBits.Guilds,
        GatewayIntentBits.GuildMessages,
        GatewayIntentBits.MessageContent,
    ]
});

// Configuration from environment variables
const SOURCE_CHANNEL_ID = process.env.SOURCE_CHANNEL_ID;
const DESTINATION_CHANNEL_ID = process.env.DESTINATION_CHANNEL_ID;
const TOKEN = process.env.DISCORD_TOKEN;

// Validate configuration
if (!TOKEN || !SOURCE_CHANNEL_ID || !DESTINATION_CHANNEL_ID) {
    console.error('❌ Error: Missing required environment variables!');
    console.error('Please check your .env file and ensure all required variables are set:');
    console.error('- DISCORD_TOKEN');
    console.error('- SOURCE_CHANNEL_ID');
    console.error('- DESTINATION_CHANNEL_ID');
    process.exit(1);
}

// Bot ready event
client.once('ready', () => {
    console.log('✅ Bot is online!');
    console.log(`📝 Logged in as: ${client.user.tag}`);
    console.log(`👀 Monitoring channel: ${SOURCE_CHANNEL_ID}`);
    console.log(`📤 Forwarding to channel: ${DESTINATION_CHANNEL_ID}`);
    console.log('-----------------------------------');
});

// Message create event - monitors all messages
client.on('messageCreate', async (message) => {
    // Ignore messages from bots (including this bot)
    if (message.author.bot) return;

    // Check if message is in the source channel
    if (message.channel.id !== SOURCE_CHANNEL_ID) return;

    try {
        // Get the destination channel
        const destinationChannel = await client.channels.fetch(DESTINATION_CHANNEL_ID);

        if (!destinationChannel) {
            console.error('❌ Could not find destination channel!');
            return;
        }

        // Get the display name (server nickname if set, otherwise username)
        const displayName = message.member?.displayName || message.author.username;
        
        // Create an embed with the message content and author info
        const embed = new EmbedBuilder()
            .setAuthor({
                name: displayName,
                iconURL: message.author.displayAvatarURL({ dynamic: true })
            })
            .setDescription(message.content || '*[No text content]*')
            .setColor('#5865F2')
            .setTimestamp(message.createdAt)
            .setFooter({ text: `User ID: ${message.author.id}` });

        // Prepare the message to send
        const forwardPayload = { embeds: [embed] };

        // If the message has attachments, add them
        if (message.attachments.size > 0) {
            const attachmentUrls = message.attachments.map(att => att.url).join('\n');
            embed.addFields({ name: '📎 Attachments', value: attachmentUrls });
        }

        // Send the message to the destination channel
        await destinationChannel.send(forwardPayload);
        
        console.log(`✅ Forwarded message from ${displayName} (${message.author.tag})`);

        // Delete the original message
        await message.delete();
        
        console.log(`🗑️  Deleted original message from ${displayName} (${message.author.tag})`);

    } catch (error) {
        console.error('❌ Error processing message:', error);
        
        // If we can't delete the message, log the specific error
        if (error.code === 50013) {
            console.error('⚠️  Missing permissions to delete messages!');
        }
    }
});

// Error handling
client.on('error', (error) => {
    console.error('❌ Discord client error:', error);
});

process.on('unhandledRejection', (error) => {
    console.error('❌ Unhandled promise rejection:', error);
});

// Login to Discord
client.login(TOKEN).catch((error) => {
    console.error('❌ Failed to login:', error);
    process.exit(1);
});
