using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sosu_remaster
{
    public record BotConfiguration
    {
        public required string Token { get; set; }
    }
}
