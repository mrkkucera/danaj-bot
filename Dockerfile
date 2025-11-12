# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY DanajBot/ ./
RUN dotnet restore
#Run build in publish mode
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
WORKDIR /app

# Copy the published app
COPY --from=build /app/publish .

# Run the bot
ENTRYPOINT ["dotnet", "DanajBot.dll"]
