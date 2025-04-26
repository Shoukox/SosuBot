using Microsoft.EntityFrameworkCore;
using SosuBot.Database.Models;
using System;
using System.Collections.Generic;

namespace SosuBot.Database;

public class BotContext : DbContext
{
    public BotContext(DbContextOptions<BotContext> options) : base(options) { }

    public DbSet<TelegramChat> TelegramChats { get; set; }
    public DbSet<OsuUser> OsuUsers { get; set; }
}
