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

Create `SosuBot/appsettings.json` and fill it with the following content:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
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
    "Postgres": "Host=localhost;Port=5432;Database=sosubot;Username=sosubot;Password=<your-password>"
  },
  "Sentry": {
    "Dsn": "",
    "Environment": "development",
    "Release": "sosubot-local",
    "EnableLogs": false,
    "TracesSampleRate": 0.1
  }
}
```

> ⚠️ Replace placeholders (`<bot-token>`, `<your-client-id>`, `<your-password>`, etc.) with your actual values.

The external score observer has a separate least-privilege configuration file at
`SosuBot.ScoresObserver/appsettings.json`. It does not need Telegram, OpenAI, or renderer secrets:

```json
{
  "OsuApiV2Configuration": {
    "ClientId": <your-client-id>,
    "ClientSecret": "<your-client-secret>"
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=sosubot;Username=sosubot;Password=<your-password>"
  }
}
```

Both files are ignored by Git and must be deployed separately.

If you start with a fresh database, apply migrations before first run:

```bash
dotnet ef database update --project SosuBot.Database --startup-project SosuBot
```

---

## 🚀 Running the Bot

Start the bot stack with Docker Compose:

First, create `.secrets/.db_password` containing only the PostgreSQL password. Compose mounts
this file into PostgreSQL and the two application images; the password does not need to be
duplicated in the container environment. Protect all local secret files:

```bash
chmod 600 .secrets/.db_password \
  SosuBot/appsettings.json \
  SosuBot.ScoresObserver/appsettings.json
```

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

Grafana is available at `http://localhost:3000`. The default login is `admin` / `admin`;
set a strong password before starting the stack in any non-local environment:

```bash
GRAFANA_ADMIN_PASSWORD='replace-with-a-strong-password' docker compose up -d --build
```

Prometheus is available at `http://localhost:9090`. The `SosuBot Overview`, database, command,
and `SosuBot Scores Observer` dashboards plus the Prometheus datasource are provisioned
automatically, so no manual Grafana setup is required.
PostgreSQL, Redis, Prometheus, and Grafana bind to `127.0.0.1` by default and are not exposed
on external interfaces.

### Local Debug with Docker infrastructure

When SosuBot runs from an IDE or through `dotnet run`, start only its infrastructure with the
debug Compose override:

```bash
docker compose -f compose.yaml -f compose.debug.yaml up -d \
  redis-service db-service prometheus grafana
```

The local bot exposes metrics on port `9091`. In this override Prometheus and Grafana use host
networking, but both web interfaces remain bound to `127.0.0.1`; Prometheus therefore reaches the
local endpoint without requiring Docker-to-host firewall rules. The regular Compose deployment
continues to use the container endpoint `sosubot-service:9090`.

If the full stack has been started before switching to local Debug, stop its bot container first:

```bash
docker compose stop sosubot-service
```

Do not run the local bot and `sosubot-service` at the same time because both instances would poll
Telegram using the same token. Stop the IDE process before returning to the full Compose stack.

Ports and the Grafana bind address can be overridden when required:

```bash
GRAFANA_BIND_ADDRESS=0.0.0.0 GRAFANA_PORT=3000 \
GRAFANA_ADMIN_PASSWORD='replace-with-a-strong-password' \
docker compose up -d --build
```

Other supported variables are `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PORT`, `REDIS_PORT`,
`PROMETHEUS_PORT`, `DOTNET_ENVIRONMENT`, `SOSUBOT_VIDEOS_PATH`, `TELEGRAM_DATA_PATH`,
`SOSUBOT_IMAGE`, `SCORES_OBSERVER_IMAGE`, `SENTRY_DSN`, `SENTRY_ENVIRONMENT`, `SENTRY_RELEASE`,
`SENTRY_OBSERVER_RELEASE`, `SENTRY_ENABLE_LOGS`, `SENTRY_TRACES_SAMPLE_RATE`, and
`SCORES_OBSERVER_CREATE_DELIVERIES`.

When launched, SosuBot will:

* Connect to Telegram using the bot token
* Initialize the osu! API client
* Start responding to Telegram commands and messages

The Compose stack also starts `scores-observer-service`. It is a separate Generic Host process
that polls osu!, stores tracked-score events and delivery records in PostgreSQL, and maintains the
daily statistics. It does not use the Telegram Bot API. SosuBot consumes the pending deliveries
from PostgreSQL and sends them to chats that still track the corresponding player.

```text
osu! API -> SosuBot.ScoresObserver -> PostgreSQL -> SosuBot -> Telegram chats
```

The observer persists a top-score checkpoint per player, per-chat subscription watermarks, and
the cursors of the global score feed. A process restart or leader failover therefore does not reset
the baseline or skip an already fetched batch. Delivery rows use renewable leases and
`FOR UPDATE SKIP LOCKED`; several bot dispatchers can process different chats while preserving
FIFO order inside one chat. Telegram itself has no idempotency key, so a crash after a successful
send but before `SentAtUtc` is committed can still cause an occasional at-least-once duplicate.

Only one observer is active at a time. PostgreSQL advisory locking keeps additional observer
instances in standby, while the main bot and its notification dispatcher may be scaled separately.

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
* `/osucard [nickname] [gamemode]` - a profile card with the player's skills.
* `/videopreview <text>` - reply to an osu! score link with this command to generate a video thumbnail.
  The text supports nested `[color=#RRGGBB]...[/color]` and `[glow=0..40]...[/glow]` BB codes.
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
The logging configuration can be customized through `appsettings.json`. Production should use
`Information` or a higher minimum level; per-update messages are logged at `Debug`.

Expected cancellation during shutdown and Telegram's benign `message is not modified` response
are not reported as application errors. Polling retries transient failures with exponential
backoff, while unexpected errors are logged once at the update boundary.

### Sentry

Both executable projects use `Sentry.Extensions.Logging`; no ASP.NET integration is required.
Create a .NET project in Sentry and pass its DSN when starting Compose:

```bash
SENTRY_DSN='https://public-key@your-sentry.example/project-id' \
SENTRY_ENVIRONMENT='production' \
SENTRY_RELEASE='sosubot@1.0.0' \
docker compose up -d --build
```

`Error` logs with exceptions appear in Sentry Issues and can be used in Sentry dashboards and
alerts. Default PII collection, automatic failed-request capture, and outbound trace propagation
are disabled. Sentry Structured Logs are also disabled by default because existing application
logs may contain user-related fields. Enable them only after reviewing the log payloads:

```bash
SENTRY_ENABLE_LOGS=true docker compose up -d
```

Do not commit DSNs, API tokens, connection strings, or passwords. `appsettings*.json` is ignored
by Git in this repository; production secrets should be supplied by Docker/Kubernetes secrets.

## 📊 Monitoring

The Docker Compose stack includes Prometheus and Grafana with 30 days of metric retention.
Three dashboards are provisioned automatically:

* **SosuBot Overview** — runtime, HTTP, Telegram throughput, queue, and active users;
* **SosuBot Database Statistics** — registered users by mode, chats by language, known members,
  tracked chats/players, persisted entities, and the latest daily country statistics;
* **SosuBot Command Statistics** — top commands, results and errors over 24 hours, error ratio,
  real-time command rate, average duration, and p95 duration.

The dashboards display:

* outgoing HTTP requests per second, minute, and hour;
* request rate by HTTP client, p95 latency, error ratio, and in-flight requests;
* Telegram update throughput, processing errors, and queue depth;
* unique active users over rolling 5-minute, 1-hour, and 24-hour windows;
* registered osu! users and known Telegram chats from PostgreSQL;
* physical `.osu` files in the configured beatmap cache directory;
* database entity totals and score-tracking subscriptions;
* latest active-user, score, and unique-beatmap daily statistics by country;
* canonical command usage, including aliases grouped under one command name and `unknown` input;
* health and latency of the periodic PostgreSQL metrics snapshot.

The bot exposes Prometheus metrics at `http://sosubot-service:9090/metrics` inside the Compose
network. A bot launched directly on the host uses port `9091` by default. Metrics can be disabled
or moved to another port through configuration:

```json
{
  "Monitoring": {
    "Enabled": true,
    "Port": 9091
  }
}
```

Active-user windows are maintained in process memory and start empty after a bot restart.
Prometheus keeps the historical time series across restarts in the `prometheus-data` volume.

Command usage is also stored in PostgreSQL in one-minute aggregates by canonical command and
result (`success`, `error`, or `cancelled`). The `AddCommandUsageStatistics` migration creates the
table. This lets the 5-minute, 1-hour, 24-hour, 7-day, and all-time panels survive bot and
Prometheus restarts. Database gauges show the current persisted state immediately, while their
time-series history starts when Prometheus first scrapes the metric.

The `AddTrackedScoreDeliveryPipeline` and `CompleteScoresObserverSplit` migrations create the
durable checkpoints, subscription watermarks, score events, global-feed cursors, tracked-score
deliveries, and daily-report deliveries. Compose applies them through the one-shot
`database-migrator` service before either application service starts. Bot replicas have automatic
migrations disabled, so they can be scaled without racing each other during startup.

For the first cutover from a release that still has the embedded observer, warm up the external
observer without producing deliveries:

```bash
# Keep the old bot running while the new schema and checkpoints are prepared.
docker compose run --rm database-migrator
SCORES_OBSERVER_CREATE_DELIVERIES=false \
  docker compose up -d --no-deps scores-observer-service

# Wait until this query returns 0.
docker compose exec -T db-service psql -U sosubot -d sosubot -Atc '
SELECT count(*)
FROM (
  SELECT DISTINCT unnest("TrackedPlayers") AS "PlayerId"
  FROM "TelegramChats"
  WHERE "TrackedPlayers" IS NOT NULL
) AS subscription
LEFT JOIN "TrackedPlayerCheckpoints" AS checkpoint
  ON checkpoint."PlayerId" = subscription."PlayerId"
WHERE checkpoint."PlayerId" IS NULL OR NOT checkpoint."IsActive";'

# Then stop the old bot, enable outbox creation, and start the new bot.
docker compose stop sosubot-service
SCORES_OBSERVER_CREATE_DELIVERIES=true \
  docker compose up -d --force-recreate scores-observer-service
docker compose up -d sosubot-service prometheus grafana
```

The warm-up mode advances checkpoints but does not create tracked-score or daily-report messages.
This keeps the old embedded observer responsible for notifications until the actual cutover.

For local Debug, the bot metrics endpoint uses port `9091` and the observer uses `9092`. Start the
observer separately when score tracking is required:

```bash
dotnet run --project SosuBot.ScoresObserver
```

Production releases now contain two application images built from the same Git revision:

```bash
release=2026.08.03-observer-split.1

docker buildx build --platform linux/amd64 -f SosuBot/Dockerfile \
  -t "shoukko/sosubot:${release}" -t shoukko/sosubot:latest --push .
docker buildx build --platform linux/amd64 -f SosuBot.ScoresObserver/Dockerfile \
  -t "shoukko/sosubot-scores-observer:${release}" \
  -t shoukko/sosubot-scores-observer:latest --push .
```

On the hosting server, update the repository as well as both images because Compose, Prometheus,
Grafana provisioning, and migrations live outside the application image:

```bash
git pull --ff-only
release=2026.08.03-observer-split.1
export SOSUBOT_IMAGE="shoukko/sosubot:${release}"
export SCORES_OBSERVER_IMAGE="shoukko/sosubot-scores-observer:${release}"

docker compose pull sosubot-service scores-observer-service database-migrator
docker compose up -d
docker compose ps
docker compose logs --since 10m database-migrator sosubot-service scores-observer-service
```


## 🧰 Technologies Used

Core frameworks and packages currently used in this repository:

* **C# / .NET 10** (`net10.0`)
* **Telegram.Bot** (`22.10.2.1`)
* **OsuApi.Core** (`0.0.512`)
* **Entity Framework Core** (`10.0.10`)
* **Npgsql.EntityFrameworkCore.PostgreSQL** (`10.0.3`)
* **Microsoft.Extensions.Http.Polly** (`10.0.10`)
* **prometheus-net** (`8.2.1`)
* **Serilog.Extensions.Logging.File** (`3.0.0`)
* **Sentry.Extensions.Logging** (`6.8.0`)
* **System.Threading.RateLimiting** (`10.0.10`)
* **ppy.osu rulesets** (`2025.1007.0`)

> Versions above are taken from the current `.csproj` files and may change over time.


## 🤝 Contributing

Contributions, pull requests, and suggestions are welcome!
Please open an issue if you encounter bugs or have feature ideas.


## 📜 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
