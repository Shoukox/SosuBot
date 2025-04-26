using OsuApi.Core.V2.Scores.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.Extensions
{
    public static class IntegerExtensions
    {
        public static string ReplaceIfNull(this int? num, string replace = "-")
        {
            return num is null ? replace : num.ToString()!;
        }
    }
}
