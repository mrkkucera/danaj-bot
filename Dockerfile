# Use Node.js Alpine as base image
FROM node:22.21.1-alpine3.21

# Set working directory
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci --only=production

# Copy application code
COPY . .

# Run the bot
CMD ["node", "index.js"]
