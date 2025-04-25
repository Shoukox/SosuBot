using Microsoft.EntityFrameworkCore;
using SosuBot.Database.Models;
using System;
using System.Collections.Generic;

namespace SosuBot.Database;

public class BotContext : DbContext
{
    public BotContext(DbContextOptions<BotContext> options) : base(options) { }

    public DbSet<TelegramChat> DiscordServers { get; set; }
}
