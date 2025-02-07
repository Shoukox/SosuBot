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
            string token = "1328219094:AAGd4s54wdzK25sxIPEMrB0gOlSRn5kY05g";
            SosuInstance si = new SosuInstance(token);
            _ = si.Start();

            while (true) { Console.ReadLine(); }
        }
    }
}
