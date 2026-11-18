# SosuBot: заметки для ИИ-агентов

Telegram-бот для игроков osu! на .NET 10 (`net10.0`, C#, nullable включён). Пользовательская документация: `README.md`
(настройка, команды, деплой, мониторинг). Здесь описано то, что нельзя быстро понять из кода.

## Карта решения (`SosuBot.sln`)

| Проект | Роль |
|---|---|
| `SosuBot` | Сам бот: Generic Host, long polling, команды, фоновые доставки уведомлений. |
| `SosuBot.ScoresObserver` | Отдельный процесс. Опрашивает osu! API и пишет события и доставки в PostgreSQL. Telegram не использует. |
| `SosuBot.Database` | `BotContext` (EF Core + Npgsql), модели, **все миграции**. Используется обоими процессами. |
| `SosuBot.Localization` | `ILocalization` и реализации `Russian` / `English` / `Deutsch`. |
| `SosuBot.Graphics` | Генерация картинок (ImageSharp): `/osucard`, превью видео. |
| `SosuBot.PerformanceCalculator` | **Git-сабмодуль** (отдельный репозиторий). Расчёт PP: официальные rulesets `ppy.osu.*` и нативная обёртка над rosu-pp на Rust. |
| `tests/*` | xunit-тесты калькулятора, графики и лимитов Telegram-контента, а также smoke-исполняемый файл для нативной библиотеки. |

Поток данных для отслеживания скоров: `osu! API -> ScoresObserver -> PostgreSQL (outbox) -> SosuBot -> Telegram`.
Доставка работает по схеме at-least-once (lease + `FOR UPDATE SKIP LOCKED`). Активен только один observer, это
обеспечивает advisory lock PostgreSQL.

## Как устроена обработка апдейтов

- `PollingBackgroundService` складывает апдейты в `UpdateQueueService`, а `UpdateHandlerBackgroundService` разбирает
  их через scoped `UpdateHandler`.
- Команды хранятся в статических словарях `UpdateHandler.Commands` / `Callbacks` и регистрируются в
  `Services/StartupServices/ConfigureBotService.cs`. После каждой команды `UpdateHandler` сам вызывает `SaveChangesAsync`.
- Сообщения без команды обрабатывает `TelegramHandlers/Text/TextHandler.cs` (ссылки на карты, AI-ответы и т.п.).
- Ошибки ловятся на границе апдейта. `message is not modified` от Telegram ожидаема и игнорируется.

### Добавление команды

1. Создать `TelegramHandlers/Commands/XxxCommand.cs`, унаследовав `CommandBase<Message>`. Нужны
   `public static readonly string[] Commands` (первый элемент становится каноническим именем для метрик) и
   `Description` (по-русски, попадает в меню Telegram).
2. Зависимости получать в `BeforeExecuteAsync` через `Context.ServiceProvider.GetRequiredService<T>()`, как в
   `OsuModeCommand.cs`.
3. Отвечать через расширения `Context.Update.ReplyAsync(Context.BotClient, text)`. Тексты по умолчанию в HTML.
4. Зарегистрировать в `ConfigureBotService`: `RegisterCommand<T>` и, если команда публичная, добавить её в массив
   `BotCommands`. Для callback'ов: `CommandBase<CallbackQuery>` + `RegisterCallback<T>`, где ключ равен первому слову
   `callback_data`.
5. Все пользовательские строки добавлять в `ILocalization` **и во все три языка**, иначе сборка не пройдёт.
   Получать через `Context.GetLocalization()`.
6. Обновить список команд в `README.md` и текст `/help` в локализации.
7. Описать команду в `SosuBot/ai-bot-commands.txt`: это справочник, который ИИ-персона Соня читает через
   инструмент `get_bot_help`. Тест `AiToolsTests` падает, если публичная команда там не описана. Скрытые и
   админские команды перечислены в тесте и в справочник не попадают.

### ИИ-персона (Соня)

- Промпт лежит в `SosuBot/developer-prompt.txt`, инструменты описаны в `Services/Ai/AiToolbox*.cs`: схемы в
  `AiToolbox.Definitions`, реализации разбиты по partial-файлам. Схемы работают в strict mode, поэтому каждое поле
  должно быть в `required`, а необязательные поля делаются nullable.
- Новые эндпоинты osu! API вызываются как сырой JSON через `ApiAsync<JsonObject>`. Длинные тексты (вики, новости)
  проходят через `AiText` и обрезаются до `AiText.MaxLongText`, чтобы не тратить токены.
- Инструменты, которые ходят в базу, открывают свой scope через `IServiceScopeFactory`, потому что `AiToolbox`
  зарегистрирован как singleton.
- `/ai` (`AiCommand`) делает то же, что `/c ai`, но пока работает только для админов, остальным отвечает
  «в разработке». Она **намеренно** не добавлена в `/help`, `BotCommands`, README и справочник ИИ. Не открывай её
  без решения владельца.

### Виджеты и rich-сообщения (Bot API 10.2+ / 10.4)

- `/userw` (`OsuUserWidgetCommand`) отправляет HTML-виджет через `SendWidget`. HTML лежит в
  `SosuBot/Widgets/user-profile.html` (embedded resource) и не меняется после отправки, данные игрока передаются в
  `public_data` (JSON, `UserProfileWidget`). Ограничения: не больше 4096 байт HTML после zlib-9, не больше 1024 байт
  на состояние, никакой сети и внешних картинок (только `data:`/inline SVG), JS работает в песочнице QuickJS. Пока
  что в проде виджеты работают **только в личке с ботом**. Справочник: https://core.telegram.org/bots/widgets.
  Лимиты проверяют тесты `TelegramRichContentTests`. Порядок полей в массиве статистики должен совпадать в C# и JS.
- Аватар и баннер игрока бот скачивает по `avatar_url`/`cover_url` (`OsuProfileImageService`, общий с `/osucard`) и
  встраивает в HTML как WebP `data:` URL (`WidgetArtwork`). `UserProfileWidget.BuildHtml` подбирает самый чёткий
  аватар, при котором виджет укладывается в `MaxCompressedHtmlBytes`. Фон карточки — размытый баннер, без баннера —
  градиент в акцентный цвет аватара, без обоих — цвета темы. Каждый байт кода в `user-profile.html` уменьшает
  разрешение аватара.
- `/lw` (`OsuLastRichCommand` → `OsuLastCommand(richMessage: true)`) строит Rich HTML в `RichLastScores`. Медиа
  можно указывать только как отдельный блок с https-URL. Если rich-сообщение не отправилось, команда отвечает
  обычным текстом `/last`.
- Бот ходит в собственный Bot API server. Пока сервер не обновлён до версии с этими методами, команды будут
  срабатывать через fallback.
- `/userw` и `/lw` зарегистрированы, но **намеренно скрыты** из `/help`, меню `BotCommands` и README: с 13.11.2026
  Telegram запретил обычным ботам `sendWidget` (`INPUT_MEDIA_WIDGET_NOT_ALLOWED`, tdlib/telegram-bot-api#947).
  Не возвращай их туда без решения владельца.
- Админские команды (`/c`, `/msg`) не отвечают тем, у кого нет доступа, чтобы не выдавать своё существование.

### Официальный расчёт pp (`PPCalculator`)

- Версия `ppy.osu.Game.Rulesets.*` должна совпадать с той, что указана в
  `ppy/osu-queue-score-statistics` (`osu.Server.Queues.ScoreStatisticsProcessor.csproj`): именно этот сервис
  начисляет pp на сайте. Сервис по умолчанию считает сложность в реальном времени той же версией пакетов.
- Для реального прошедшего счёта `OfficialPerformanceHelper` передаёт полную `statistics`, `maximum_statistics` и
  `legacy_total_score`. Тогда движок не оценивает попадания, а берёт статистику как есть и считает accuracy так же,
  как osu! (`StandardisedScoreMigrationTools.ComputeAccuracy`). Без `legacy_total_score` pp stable-счётов osu!
  расходится с сайтом на десятки pp. Для оценок вроде if-FC, `/calc` и превью карт статистика генерируется в
  `AccuracyTools`.
- Сверено на 288 реальных счётах всех режимов: расхождение с сайтом не больше 0.1pp. Сервер не перезаписывает pp
  счёта, если тот изменился меньше чем на 0.1, так что старые счёты на сайте могут отставать на эту величину.
- `PPCalculationCache` хранит только подготовленные данные (атрибуты сложности и счётчики объектов, 1–2 КБ),
  ограничен `PPCalculationCacheOptions.MaxEntries` и вытесняет по LRU. Ключ строится из SHA-256 файла, а не из ID
  карты. **Не кешируй `WorkingBeatmap`/`IBeatmap`**: они весят мегабайты, и прошлый неограниченный кеш занимал около
  2 ГБ на 236 карт.
- `BeatmapsService.DownloadOrCacheBeatmapAsync(id, checksum, …)` скачивает файл заново, если MD5 кешированного
  `.osu` не совпадает с `checksum` из API. Так бот подхватывает обновлённые неранкнутые карты.

## База данных и миграции

- Миграции лежат в `SosuBot.Database/Migrations`, стартовый проект `SosuBot`:
  `dotnet ef migrations add <Name> --project SosuBot.Database --startup-project SosuBot`.
- В Compose миграции применяет одноразовый сервис `database-migrator` (`Database:MigrateOnly=true`). У реплик бота
  `MigrateOnStartup=false`. Локально по умолчанию миграции применяются при старте.
- `PendingModelChangesWarning` намеренно подавлен. Некоторые миграции написаны вручную без `.Designer.cs`
  (например, `AddBlockedTopicIdsToTelegramChats`), поэтому после `migrations add` проверяй, что снапшот и SQL
  не содержат лишних изменений.
- Включены `Npgsql.EnableLegacyTimestampBehavior` и lazy-loading proxies, поэтому навигационные свойства должны быть
  `virtual`. Сложные объекты (`User`, `Score`, `RenderSettings`) хранятся в `jsonb` через `ValueConverter`.
- `BotContext` содержит seed первого админа (`OsuUser.IsAdmin`).

## Сборка, тесты, конфиги

- `SosuBot` и `SosuBot.ScoresObserver` собираются с `TreatWarningsAsErrors=true`. Это касается и NuGet Audit:
  пакет с известной уязвимостью (NU1902/NU1903) **ломает restore**. Если сборка внезапно упала, первым делом проверь
  `dotnet list package --vulnerable --include-transitive`.
- Тесты: `dotnet test SosuBot.sln -c Release`. Smoke-тест нативного калькулятора:
  `dotnet run --project tests/SosuBot.PerformanceCalculator.Smoke -c Release`. Для него нужен собранный
  `librosu_pp_native.so` в `SosuBot.PerformanceCalculator/Calculators/Rosu/runtimes/<rid>/native/`.
- `appsettings*.json` есть в `.gitignore` и не попадают в образ (`.dockerignore`). Их монтирует Compose. Реальные
  секреты лежат в этих файлах и в `.secrets/`: **не читай их вывод в ответы и не коммить**. Шаблон конфига есть
  в `README.md`.
- Локальный запуск бота опрашивает Telegram тем же токеном, что и прод. Пока работает `sosubot-service`, бота
  локально не запускай.

## Деплой

На хостинге всё работает через `docker compose` (`compose.yaml`): bot, observer, `database-migrator`, PostgreSQL 17,
Redis 7.4, Prometheus и Grafana. Бот ходит в Telegram через собственный Bot API server (`BotConfiguration:ApiServerUrl`,
см. `SosuBot/tg-server.txt`). Образ бота собирает Rust-стейдж (`rust:1.94.0-bookworm`) для rosu-pp. Порядок выпуска
релиза описан в разделе README про production releases.

## Известные подводные камни

- **Сабмодуль `SosuBot.PerformanceCalculator`**: правки в нём (включая `.csproj`) нужно коммитить в репозиторий
  сабмодуля, а потом обновлять указатель сабмодуля в основном репозитории.
- **Регистр путей.** Rust-проект лежит в `Calculators/Rosu/Native/rosu-pp-native/` (заглавная `N`, рядом с C#
  interop-кодом), а готовые библиотеки — в `Calculators/Rosu/runtimes/<rid>/native/` (строчная). Dockerfile, CI,
  `.gitignore` и `.csproj` должны писать путь именно так. Держи `core.ignorecase=false` в обоих репозиториях: при
  `true` на Linux git молча связывает `native/` с `Native/`, и в коммит попадает не тот путь.
- **ImageSharp 3.2.0 / Drawing 2.2.0 требуют лицензию Six Labors.** Без ключа Release-сборка `SosuBot.Graphics`
  (единственного проекта с прямой ссылкой на эти пакеты) падает на таргете `SixLabors_ValidateLicense`. Ключ не
  коммитится: `Directory.Build.props` берёт его из `$(SixLaborsLicenseKey)` (свойство или переменная окружения) или
  из `.secrets/sixlabors.lic`. Docker-сборка бота монтирует файл как BuildKit secret `sixlabors_license` (Compose:
  `build.secrets`, CI: секрет репозитория `SIXLABORS_LICENSE_KEY`). Остаёмся на линейке 3.x, потому что `ppy.osu.Game`
  собран под ImageSharp 3.1.x. Декодирование изображений всё равно идёт только через `SafeImageDecoding.Load`
  (только PNG/JPEG/GIF/WebP, без метаданных и ICC).
- **Шрифты в `SosuBot.Graphics/Fonts`.** В Quicksand и Aller нет кириллицы. Для картинок с русским текстом бери
  Nunito (`Fonts/Nunito`, SIL OFL, `OFL.txt` лежит рядом). На нём сделаны карточка реплея (`ReplayCardGenerator`,
  ответ на присланный `.osr`), `/userp` (`UserProfileCardGenerator`) и `/scorep` (`ScoreCardGenerator`). Полоса скора и
  ряды статистики общие (`ScoreStatsPainter`). SixLabors.Fonts не умеет вариативные шрифты, поэтому нужны статические
  TTF.
- **Множители скора модов.** В `ppy.osu.Game` 2026.x у каждого мода `Mod.ScoreMultiplier` равен 1. Настоящий
  множитель считает `ruleset.CreateScoreMultiplierCalculator(new ScoreMultiplierContext(difficulty, scoreInfo))
  .CalculateFor(mods)`, и он зависит от рулсета и сложности карты (пример — `ReplayCardService.MaxStandardisedScore`).
- **Статистика реплея.** Обычные счётчики `.osr` не содержат концов слайдеров и тиков, поэтому pp по ним расходится с
  сайтом. osu!lazer хранит полный результат в LZMA-блоке после кадров (`ReplayFileCompatibility.ReadScore`), для
  остальных реплеев `ReplayCardService` берёт его из API: `GET /scores/{ruleset}/{legacy id}`. Файлы присылают чужие
  люди, поэтому размер словаря LZMA и распакованных данных ограничен.
- `Directory.Build.props` в корне действует на все проекты, включая сабмодуль. Dockerfile копируют его перед
  `dotnet restore`. При добавлении нового Dockerfile сделай так же.
- Остаёмся на .NET 10 LTS: EF Core 11 требует `net11.0`, а стабильного `Npgsql.EntityFrameworkCore.PostgreSQL` 11 пока
  нет. Пакеты `Microsoft.*` / `System.*` держим на одной патч-версии 10.0.x.
- Лимиты частоты (`RateLimiterFactory`) хранятся в Redis. Ключ бакета — `token_bucket:{имя политики}:{ключ}`.
  У новой политики должно быть своё имя, иначе она разделит бакет с другой. Бакет хранит дробные токены и живёт
  столько, сколько нужно на полное пополнение, поэтому медленные лимиты вроде «10 в час» работают.
- `AutoMapper` подключён в `SosuBot` напрямую, только чтобы перекрыть уязвимую транзитивную 13.0.1 из `ppy.osu.Game`.
  В коде он не используется, но ссылку нужно сохранить.
