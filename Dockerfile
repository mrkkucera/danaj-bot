# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY DanajBot/ Directory.Build.props Directory.Packages.props ./
RUN dotnet restore
#Run build in publish mode
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app

# Install ICU libraries for globalization support
RUN apk add --no-cache icu-libs

# Disable globalization-invariant mode
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Set timezone to Europe/Prague
ENV TZ=Europe/Prague

# Copy the published app
COPY --from=build /app/publish .

# Run the bot
ENTRYPOINT ["dotnet", "DanajBot.dll"]
