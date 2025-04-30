using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.OsuTypes
{
    public record PPResult
    {
        public required double? Current { get; set; }
        public required double? IfSS { get; set; }
    }
}
