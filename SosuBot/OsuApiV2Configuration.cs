using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SosuBot
{
    public record OsuApiV2Configuration
    {
        public required int ClientId { get; init; }
        public required string ClientSecret { get; init; }
    }
}
