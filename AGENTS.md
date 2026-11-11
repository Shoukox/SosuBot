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
| `tests/*` | xunit-тесты калькулятора и графики, а также smoke-исполняемый файл для нативной библиотеки. |

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
  сабмодуля, а потом обновлять указатель сабмодуля в основном репозитории. Каталог
  `Calculators/Rosu/native/rosu-pp-native/` сейчас не отслеживается в сабмодуле, а Dockerfile и CI его ожидают.
- CI (`.github/workflows/dotnet.yml`) ссылается на `SosuBot.PerformanceCalculator/native/rosu-pp-native` и
  `.../runtimes/linux-x64`, а реальный путь `Calculators/Rosu/native/...` и `Calculators/Rosu/runtimes/...`.
- **ImageSharp закреплён на 3.1.12 / Drawing 2.1.7.** Начиная с ImageSharp 3.2.0 / 4.x и Drawing 2.2.0 / 3.x
  Release-сборка требует лицензионный ключ Six Labors (`SixLaborsLicenseKey` или `sixlabors.lic`), иначе падает
  таргет `SixLabors_ValidateLicense`. Известные CVE версии 3.1.x подавлены в `Directory.Build.props` и закрыты на
  уровне кода в `SosuBot.Graphics/SafeImageDecoding.cs`: только PNG/JPEG/GIF/WebP, метаданные и ICC не разбираются.
  Любое декодирование изображений должно идти через `SafeImageDecoding.Load`. Если появится ключ и вы перейдёте на
  новые версии, удалите подавления. `ppy.osu.Game` тоже использует ImageSharp 3.1.x.
- `Directory.Build.props` в корне действует на все проекты, включая сабмодуль. Dockerfile копируют его перед
  `dotnet restore`. При добавлении нового Dockerfile сделай так же.
- Остаёмся на .NET 10 LTS: EF Core 11 требует `net11.0`, а стабильного `Npgsql.EntityFrameworkCore.PostgreSQL` 11 пока
  нет. Пакеты `Microsoft.*` / `System.*` держим на одной патч-версии 10.0.x.
- `AutoMapper` подключён в `SosuBot` напрямую, только чтобы перекрыть уязвимую транзитивную 13.0.1 из `ppy.osu.Game`.
  В коде он не используется, но ссылку нужно сохранить.
