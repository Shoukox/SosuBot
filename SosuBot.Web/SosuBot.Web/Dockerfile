FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SosuBot.Web/SosuBot.Web/SosuBot.Web.csproj", "SosuBot.Web/SosuBot.Web/"]
COPY ["SosuBot.Web/SosuBot.Web.Client/SosuBot.Web.Client.csproj", "SosuBot.Web/SosuBot.Web.Client/"]
RUN dotnet restore "SosuBot.Web/SosuBot.Web/SosuBot.Web.csproj"
COPY . .
WORKDIR "/src/SosuBot.Web/SosuBot.Web"
RUN dotnet build "./SosuBot.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SosuBot.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SosuBot.Web.dll"]
