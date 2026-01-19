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

        // Convert render settings
        var renderSettingsComparer = new ValueComparer<DanserConfiguration>(
            (l, r) => JsonSerializer.Serialize(l) == JsonSerializer.Serialize(r),
            v => JsonSerializer.Serialize(v).GetHashCode(),
            v => JsonSerializer.Deserialize<DanserConfiguration>(
                    JsonSerializer.Serialize(v))!
        );
        var renderSettingsConverter = new ValueConverter<DanserConfiguration, string>(
            v => JsonSerializer.Serialize(v, jsonConfig),
            v => JsonSerializer.Deserialize<DanserConfiguration>(v)!);
        modelBuilder.Entity<OsuUser>()
            .Property(e => e.RenderSettings)
            .HasConversion(renderSettingsConverter, renderSettingsComparer)
            .HasColumnType("jsonb");

        // Many-to-many relationships
        modelBuilder.Entity<DailyStatistics>()
            .HasMany(m => m.Scores)
            .WithOne();
        modelBuilder.Entity<DailyStatistics>()
            .HasMany(m => m.ActiveUsers)
            .WithMany();

        // Allow datetimes with unspecified time zones
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }
}