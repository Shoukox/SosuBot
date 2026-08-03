using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Database.Models;
using SosuBot.Database.Models;
using System.Text.Json;

namespace SosuBot.Database;

/// <summary>
///     bot.db
/// </summary>
public class BotContext : DbContext
{
    public BotContext(DbContextOptions<BotContext> options) : base(options)
    {

    }

    public DbSet<TelegramChat> TelegramChats { get; set; }
    public DbSet<OsuUser> OsuUsers { get; set; }
    public DbSet<DailyStatistics> DailyStatistics { get; set; }
    public DbSet<UserEntity> UserEntity { get; set; }
    public DbSet<ScoreEntity> ScoreEntity { get; set; }
    public DbSet<CommandUsageAggregate> CommandUsageAggregates { get; set; }
    public DbSet<TrackedScoreEvent> TrackedScoreEvents { get; set; }
    public DbSet<TrackedScoreDelivery> TrackedScoreDeliveries { get; set; }
    public DbSet<TrackedPlayerCheckpoint> TrackedPlayerCheckpoints { get; set; }
    public DbSet<DailyStatisticsReportDelivery> DailyStatisticsReportDeliveries { get; set; }
    public DbSet<TrackedPlayerSubscription> TrackedPlayerSubscriptions { get; set; }
    public DbSet<ScoreFeedCheckpoint> ScoreFeedCheckpoints { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Init the first admin
        modelBuilder.Entity<OsuUser>().HasData(new OsuUser
        {
            OsuUserId = 15319810,
            OsuMode = Playmode.Osu,
            OsuUsername = "Shoukko",
            IsAdmin = true,
            TelegramId = 728384906
        });

        var jsonConfig = new JsonSerializerOptions() { WriteIndented = false };

        // Convert User
        var userConverter = new ValueConverter<User, string>(
            v => JsonSerializer.Serialize(v, jsonConfig),
            v => JsonSerializer.Deserialize<User>(v)!);
        modelBuilder.Entity<UserEntity>()
            .Property(e => e.UserJson)
            .HasConversion(userConverter)
            .HasColumnType("jsonb");

        // Convert Score
        var scoreConverter = new ValueConverter<Score, string>(
            v => JsonSerializer.Serialize(v, jsonConfig),
            v => JsonSerializer.Deserialize<Score>(v)!);
        modelBuilder.Entity<ScoreEntity>()
            .Property(e => e.ScoreJson)
            .HasConversion(scoreConverter)
            .HasColumnType("jsonb");
        modelBuilder.Entity<TrackedScoreEvent>()
            .Property(e => e.ScoreJson)
            .HasConversion(scoreConverter)
            .HasColumnType("jsonb");

        modelBuilder.Entity<TrackedScoreEvent>()
            .HasIndex(e => new { e.PlayerId, e.OccurredAtUtc });
        modelBuilder.Entity<TrackedScoreDelivery>()
            .HasIndex(e => new { e.AvailableAtUtc, e.LockedUntilUtc })
            .HasDatabaseName("IX_TrackedScoreDeliveries_Pending")
            .HasFilter("\"SentAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"FailedAtUtc\" IS NULL");
        modelBuilder.Entity<TrackedScoreDelivery>()
            .HasOne(e => e.Event)
            .WithMany(e => e.Deliveries)
            .HasForeignKey(e => e.ScoreId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DailyStatisticsReportDelivery>()
            .Property(delivery => delivery.Mode)
            .HasConversion<int>()
            .HasColumnType("integer");
        modelBuilder.Entity<DailyStatisticsReportDelivery>()
            .HasIndex(delivery => new { delivery.AvailableAtUtc, delivery.LockedUntilUtc })
            .HasDatabaseName("IX_DailyStatisticsReportDeliveries_Pending")
            .HasFilter("\"SentAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"FailedAtUtc\" IS NULL");
        modelBuilder.Entity<DailyStatisticsReportDelivery>()
            .HasOne(delivery => delivery.DailyStatistics)
            .WithMany()
            .HasForeignKey(delivery => delivery.DailyStatisticsId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TrackedPlayerSubscription>()
            .HasIndex(e => e.PlayerId);
        modelBuilder.Entity<TrackedPlayerSubscription>()
            .HasOne(e => e.Chat)
            .WithMany()
            .HasForeignKey(e => e.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        // Convert render settings
        var renderSettingsComparer = new ValueComparer<RenderSettings>(
            (l, r) => JsonSerializer.Serialize(l) == JsonSerializer.Serialize(r),
            v => JsonSerializer.Serialize(v).GetHashCode(),
            v => JsonSerializer.Deserialize<RenderSettings>(
                    JsonSerializer.Serialize(v))!
        );
        var renderSettingsConverter = new ValueConverter<RenderSettings, string>(
            v => JsonSerializer.Serialize(v, jsonConfig),
            v => JsonSerializer.Deserialize<RenderSettings>(v)!);
        modelBuilder.Entity<OsuUser>()
            .Property(e => e.RenderSettings)
            .HasConversion(renderSettingsConverter, renderSettingsComparer)
            .HasColumnType("jsonb");

        modelBuilder.Entity<DailyStatistics>()
            .Property(e => e.DayOfStatistic)
            .HasColumnType("timestamp without time zone");

        // Many-to-many relationships
        modelBuilder.Entity<DailyStatistics>()
            .HasMany(m => m.Scores)
            .WithOne()
            .HasForeignKey(m => m.DailyStatisticsId);
        modelBuilder.Entity<DailyStatistics>()
            .HasMany(m => m.ActiveUsers)
            .WithMany();

        // Allow datetimes with unspecified time zones
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }
}
