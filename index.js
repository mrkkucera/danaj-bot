require('dotenv').config();
const { Client, GatewayIntentBits, EmbedBuilder, PermissionFlagsBits } = require('discord.js');

// Create a new Discord client with necessary intents
const client = new Client({
    intents: [
        GatewayIntentBits.Guilds,
        GatewayIntentBits.GuildMessages,
        GatewayIntentBits.MessageContent,
        GatewayIntentBits.GuildMessageReactions,
    ],
    partials: ['MESSAGE', 'CHANNEL', 'REACTION']
});

// Configuration from environment variables
const SOURCE_CHANNEL_ID = process.env.SOURCE_CHANNEL_ID;
const DESTINATION_CHANNEL_ID = process.env.DESTINATION_CHANNEL_ID;
const TOKEN = process.env.DISCORD_TOKEN;

// Command constants
const ZKOUSKA_COMMAND = '!zkouska';

// Store class message IDs to track reactions and their associated thread IDs
const classMessages = new Map(); // messageId -> threadId

// Track users who have already reacted to each zkouska
const userReactions = new Map(); // messageId -> Set of userIds

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
client.once('clientReady', () => {
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

    // Handle !class command for moderators
    if (message.content.startsWith(`${ZKOUSKA_COMMAND} `)) {
        // Check if user has moderator permissions (MANAGE_MESSAGES)
        if (!message.member.permissions.has(PermissionFlagsBits.ManageMessages)) {
            await message.reply('❌ Potřebujete moderátorské oprávnění k vytvoření zkoušky!');
            return;
        }

        // Extract class description
        const description = message.content.slice(ZKOUSKA_COMMAND.length + 1).trim();
        
        if (!description) {
            await message.reply(`❌ Chybí popis! Použití: \`${ZKOUSKA_COMMAND} <description>\``);
            return;
        }

        try {
            // Get the destination channel first
            const destinationChannel = await client.channels.fetch(DESTINATION_CHANNEL_ID);

            if (!destinationChannel) {
                console.error('❌ Could not find destination channel!');
                await message.reply('❌ Chyba: Nelze najít cílový kanál.');
                return;
            }

            // Create a regular message for the class
            const classMessageText = `📚 **Zkouška**\n\n${description}\n\n*Reagujte pomocí ❌ pokud se chcete omluvit z této zkoušky. Vaše reakce po chvilce zmizí, ale bude zaznamenána.*`;

            // Send the class message
            const classMessage = await message.channel.send(classMessageText);
            
            // Add the default reaction
            await classMessage.react('❌');
            
            // Create a thread in the destination channel for this zkouska
            const threadName = description.length > 100 ? `${description.substring(0, 97)}...` : description;
            const thread = await destinationChannel.threads.create({
                name: `📚 ${threadName}`,
                autoArchiveDuration: 10080, // 7 days
                reason: `Thread for zkouska: ${description}`
            });
            
            // Store the message ID and thread ID mapping
            classMessages.set(classMessage.id, thread.id);
            
            // Initialize the user reactions set for this zkouska
            userReactions.set(classMessage.id, new Set());
            
            // Delete the command message
            await message.delete();
            
            console.log(`✅ ${message.author.tag} vytvoril zkousku: ${description}`);
            console.log(`✅ Created thread: ${thread.name} (${thread.id})`);
        } catch (error) {
            console.error('❌ Chyba pri vytvareni zkousky:', error);
            await message.reply('❌ Chyba pri vytvareni zkousky. Zkuste to prosím znovu.');
        }
        
        return;
    }
});

// Reaction add event - handles reactions to class messages
client.on('messageReactionAdd', async (reaction, user) => {
    // Ignore bot reactions
    if (user.bot) return;

    // Fetch the reaction and message if they're partial
    if (reaction.partial) {
        try {
            await reaction.fetch();
        } catch (error) {
            console.error('❌ Error fetching reaction:', error);
            return;
        }
    }

    if (reaction.message.partial) {
        try {
            await reaction.message.fetch();
        } catch (error) {
            console.error('❌ Error fetching message:', error);
            return;
        }
    }

    // Check if this is a class message
    if (!classMessages.has(reaction.message.id)) return;

    // Check if the reaction is ❌
    if (reaction.emoji.name !== '❌') return;

    try {
        // Get the thread ID for this zkouska
        const threadId = classMessages.get(reaction.message.id);
        
        if (!threadId) {
            console.error('❌ Thread ID not found for this zkouska message!');
            return;
        }

        // Check if this user has already reacted to this zkouska
        const reactedUsers = userReactions.get(reaction.message.id);
        
        if (reactedUsers && reactedUsers.has(user.id)) {
            console.log(`⚠️  ${user.tag} has already reacted to this zkouska, skipping...`);
            // Still remove the reaction to keep the UI clean
            await reaction.users.remove(user.id);
            return;
        }

        // Fetch the thread
        const thread = await client.channels.fetch(threadId);

        if (!thread) {
            console.error('❌ Could not find thread!');
            return;
        }

        // Get the member who reacted
        const member = await reaction.message.guild.members.fetch(user.id);
        const displayName = member.displayName || user.username;

        // Get the class description from the message content
        // Extract text between the title and the instruction text
        const messageContent = reaction.message.content;
        const descriptionMatch = messageContent.match(/📚 \*\*Zkouška\*\*\n\n(.*?)\n\n\*/s);
        const classDescription = descriptionMatch ? descriptionMatch[1] : 'Unknown class';

        // Create an embed to send to the thread
        const notificationEmbed = new EmbedBuilder()
            .setAuthor({
                name: displayName,
                iconURL: user.displayAvatarURL({ dynamic: true })
            })
            .setDescription(`**Omlouvenka ze zkoušky:**\n${classDescription}`)
            .setColor('#5865F2')
            .setTimestamp();

        // Send to the thread
        await thread.send({ embeds: [notificationEmbed] });
        
        // Mark this user as having reacted
        if (reactedUsers) {
            reactedUsers.add(user.id);
        }
        
        console.log(`✅ ${displayName} (${user.tag}) se omluvil ze zkousky`);

        // Remove the user's reaction to restore the default state
        await reaction.users.remove(user.id);
        
        console.log(`🔄 Removed reaction from ${displayName} to hide count`);

    } catch (error) {
        console.error('❌ Error processing reaction:', error);
        
        // If we can't remove the reaction, log the specific error
        if (error.code === 50013) {
            console.error('⚠️  Missing permissions to manage reactions!');
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
