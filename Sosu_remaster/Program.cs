using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sosu_remaster
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string token = "1776414200:AAGlVI_yhVNxkjNjKUOepvtI8hhyd4HZjOE";
            SosuInstance si = new SosuInstance(token);
            si.Start();

            while (true) { Console.ReadLine(); }
        }
    }
}
