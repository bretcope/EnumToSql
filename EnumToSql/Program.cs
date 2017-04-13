using System;

namespace EnumToSql
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
