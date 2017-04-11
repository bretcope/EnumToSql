
using System;
using System.IO;

namespace EnumsToSql
{
    class Program
    {
        static void Main(string[] args)
        {
            var success = Cli.Execute(args, Console.Out);
            Environment.Exit(success ? 0 : 1);
        }
    }
}
