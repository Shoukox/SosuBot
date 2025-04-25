using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.Logging
{
    internal class CustomConsoleFormatterOptions : ConsoleFormatterOptions
    {
        public string? CustomPrefix { get; set; }   
    }
}
