# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SosuBot.ScoresObserver/SosuBot.ScoresObserver.csproj", "SosuBot.ScoresObserver/"]
COPY ["SosuBot.Database/SosuBot.Database.csproj", "SosuBot.Database/"]
RUN dotnet restore "./SosuBot.ScoresObserver/SosuBot.ScoresObserver.csproj"
COPY . .
WORKDIR "/src/SosuBot.ScoresObserver"
RUN dotnet build "./SosuBot.ScoresObserver.csproj" -c $BUILD_CONFIGURATION --no-restore

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SosuBot.ScoresObserver.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-build

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
USER root
WORKDIR /app
RUN mkdir -p /app/cache /app/logs \
    && chown -R "$APP_UID:$APP_UID" /app/cache /app/logs
COPY --from=publish /app/publish .
COPY --chmod=0755 SosuBot.ScoresObserver/docker-entrypoint.sh /usr/local/bin/sosubot-scores-observer-entrypoint
ENTRYPOINT ["/usr/local/bin/sosubot-scores-observer-entrypoint"]
CMD ["dotnet", "SosuBot.ScoresObserver.dll"]
