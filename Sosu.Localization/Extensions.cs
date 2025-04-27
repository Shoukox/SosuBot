using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sosu.Localization
{
    public static class Extensions
    {
        public static string Fill(this string text, IEnumerable<string> replace)
        {
            foreach (var item in replace)
            {
                int ind = Regex.Match(text, @"{(.*)}").Index;
                text = text.Remove(ind, 2).Insert(ind, item);
            }
            return text;
        }
    }
}
