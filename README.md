# SosuBot (osu! helper)

**A Telegram bot for osu! players** — built with .NET 8 and designed to make interacting with osu! data fun and seamless.

---

## 🧩 Overview

SosuBot is a **Telegram bot** that connects to the **osu! API v2**, providing player statistics, recent plays, and other osu!-related data directly through Telegram chats.
It also uses **OpenAI API** for smart features such as message analysis, playful responses, or extended contextual queries.

---

## ⚙️ Requirements

Before running the bot, make sure you have the following installed:

* **.NET SDK 8.0 or higher**
  [Download .NET SDK 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

---

## 📁 Setup

### 1. Clone the repository

```bash
git clone https://github.com/Shoukox/SosuBot.git
cd SosuBot
```

### 2. Configure OpenAI

Create a file named `openai-settings.json` in the project root and insert your OpenAI token:

```json
{
  "OpenAiConfiguration": {
    "Token": "<your_token_here>",
    "DeveloperPrompt": "<prompt>",
    "Model": "gpt-4o-mini"
  }
}
```

### 3. Configure Application Settings

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
    "DefaultConnection": "Data Source=bot.db"
  }
}
```

> ⚠️ Replace placeholders (`<bot-token>`, `<your-client-id>`, etc.) with your actual values.

---

## 🚀 Running the Bot

### Using the .NET CLI

```bash
dotnet run --project SosuBot
```

### Or build and run

```bash
dotnet build -c Release
dotnet path/to/release/SosuBot.dll
```

When launched, SosuBot will:

* Connect to Telegram using the bot token
* Initialize the osu! API client
* Use the OpenAI API for any AI-based features
* Start responding to Telegram commands and messages

Use the /help command to get a summary about bots functionality.
---

## 🧠 Features (in brief)

* osu! player stats lookup
* Recent plays and performance tracking
* Some special features for 🇺🇿 players 
* Integration with OpenAI for contextual replies
* Custom logging system (console + file)
* SQLite database for local data storage

---

## 🪲 Logging

Logs are written to console and to daily files in the `logs/` folder (e.g., `logs/2025-10-19.log`).
The logging configuration can be customized through `appsettings.json`.

---

## 🧰 Technologies Used (brief)

* **C# / .NET 8**
* **Telegram.Bot**
* **osu! API v2**
* **OpenAI API**
* **SQLite**
* **Polly** (for HTTP resiliency)
* **Microsoft.Extensions.Logging**
* ...

---

## 🤝 Contributing

Contributions, pull requests, and suggestions are welcome!
Please open an issue if you encounter bugs or have feature ideas.

---

## 📜 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
