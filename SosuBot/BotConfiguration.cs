using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot;

public record BotConfiguration
{
    public required string Token { get; init; }
}