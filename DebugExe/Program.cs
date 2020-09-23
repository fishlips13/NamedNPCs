using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebugExe
{
    class Program
    {
        static void Main(string[] args)
        {
            string hello = "\"hello";

            string t = hello.Replace("\"", "");

            Console.WriteLine(t);
            Console.ReadKey();
        }
    }
}
