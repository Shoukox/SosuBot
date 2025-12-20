using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Helpers.Types;

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

        // Convert User
        var userConverter = new ValueConverter<User, string>(
            v => JsonConvert.SerializeObject(v, Formatting.None),
            v => JsonConvert.DeserializeObject<User>(v)!);
        modelBuilder.Entity<UserEntity>()
            .Property(e => e.UserJson)
            .HasConversion(userConverter)
            .HasColumnType("jsonb");

        // Convert Score
        var scoreConverter = new ValueConverter<Score, string>(
            v => JsonConvert.SerializeObject(v, Formatting.None),
            v => JsonConvert.DeserializeObject<Score>(v)!);
        modelBuilder.Entity<ScoreEntity>()
            .Property(e => e.ScoreJson)
            .HasConversion(scoreConverter)
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