using Microsoft.EntityFrameworkCore;
using SosuBot.Database.Models;

namespace SosuBot.Database;

public class BotContext : DbContext
{
    public BotContext(DbContextOptions<BotContext> options) : base(options) { }

    public DbSet<TelegramChat> TelegramChats { get; set; }
    public DbSet<OsuUser> OsuUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OsuUser>().HasData(new OsuUser()
        {
            OsuUserId = 15319810,
            OsuMode = OsuTypes.Playmode.Osu,
            OsuUsername = "Shoukko",
            IsAdmin = true,
            TelegramId = 728384906
        });
    }
}
