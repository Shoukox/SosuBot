# SosuBot (osu! helper)

**A Telegram bot for osu! players** — built with .NET 10 and designed to make interacting with osu! data fun and seamless.


## 🧩 Overview

SosuBot is a **Telegram bot** that connects to the **osu! API v2**, providing player statistics, recent plays, and other osu!-related data directly through Telegram chats.
It also includes replay rendering features, chat statistics, and tracking workflows tailored for active osu! communities.


## ⚙️ Requirements

Before running the bot, make sure you have the following installed:

* **.NET SDK 10.0 or higher**
  [Download .NET SDK](https://dotnet.microsoft.com/en-us/download)
  

## 📁 Setup

### 1. Clone the repository

```bash
git clone https://github.com/Shoukox/SosuBot.git
cd SosuBot
```

### 2. Configure Application Settings

Create a file named `appsettings.json` in the root directory and fill it with the following content:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Error",
      "System.Net.Http.HttpClient": "Error"
    },
    "Console": {
      "FormatterName": "CustomConsoleFormatter",
      "FormatterOptions": {
        "IncludeScopes": true,
        "TimestampFormat": "HH:mm:ss.fff",
        "UseUtcTimestamp": true,
        "SingleLine": true
      }
    }
  },
  "BotConfiguration": {
    "Token": "<bot-token>",
    "Username": "<bot-username>"
  },
  "OsuApiV2Configuration": {
    "ClientId": <your-client-id>,
    "ClientSecret": "<your-client-secret>"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=sosubot;Username=sosubot;Password=<your-password>"
  }
}
```

> ⚠️ Replace placeholders (`<bot-token>`, `<your-client-id>`, `<your-password>`, etc.) with your actual values.

If you start with a fresh database, apply migrations before first run:

```bash
dotnet ef database update --project SosuBot.Database --startup-project SosuBot
```

---

## 🚀 Running the Bot

Start the bot stack with Docker Compose:

```bash
docker compose up -d --build
```

Useful commands:

```bash
# View logs
docker compose logs -f

# Stop services
docker compose down
```

When launched, SosuBot will:

* Connect to Telegram using the bot token
* Initialize the osu! API client
* Start responding to Telegram commands and messages

Use the `/help` command to get a summary about bot functionality.

## 🧠 Bot Commands

Command list from the current in-bot `/help` text:

* `/set [nickname]` - add/change your nickname in the bot.
* `/mode [gamemode]` - change your default game mode.
* `/user [nickname]` - short info about a player by username.
* `/userid [user_id]` - short info about a player by user id.
* `/last [nickname] [count]` - latest plays.
* `/lastpassed [nickname] [count]` - `/last` for passed scores only.
* `/score [beatmap_link]` - your records on this map.
* `/userbest [nickname] [gamemode]` - player's best plays.
* `/compare [nickname1] [nickname2] [gamemode]` - compare players.
* `/chatstats [gamemode]` - top 10 players in this chat.
* `/exclude [nickname]` - exclude a user from chat top 10.
* `/include [nickname]` - include a user back into chat top 10.
* `/ranking [RU/UZ/country_code]` - top 20 players for a country (or global).
* `/daily_stats` - Uzbekistan exclusive: daily stats for all scores from all players in the country.
* `/track [users1-3]` - bot notifies you about new top50 scores of these players.
* `/render` - replay rendering.
* `/settings` - replay renderer settings.
* `/setskin` - send your skin to the bot.
* `/info` - latest info about your osu profile from the bot.
* `/lang` - change bot language.

Additional behavior from help text:

* If you send a beatmap link, the bot sends short map information.
* To prevent that, add `-` at the end of the beatmap link.


## 🪲 Logging

Logs are written to console and to daily files in the `logs/` folder (e.g., `logs/2025-10-19.log`).
The logging configuration can be customized through `appsettings.json`.


## 🧰 Technologies Used

Core frameworks and packages currently used in this repository:

* **C# / .NET 10** (`net10.0`)
* **Telegram.Bot** (`22.8.1` in main bot, `22.5.1` in ScoresObserver)
* **OsuApi.Core** (`0.0.512`)
* **Entity Framework Core** (`10.0.1`)
* **Npgsql.EntityFrameworkCore.PostgreSQL** (`10.0.0`)
* **Polly** (`8.6.5`) + **Microsoft.Extensions.Http.Polly** (`10.0.1`)
* **Serilog.Extensions.Logging.File** (`3.0.0`)
* **System.Threading.RateLimiting** (`10.0.1`)
* **ppy.osu rulesets** (`2025.1007.0`)

> Versions above are taken from the current `.csproj` files and may change over time.


## 🤝 Contributing

Contributions, pull requests, and suggestions are welcome!
Please open an issue if you encounter bugs or have feature ideas.


## 📜 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
