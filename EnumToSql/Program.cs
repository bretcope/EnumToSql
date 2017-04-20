using System;

namespace EnumToSql
{
    class Program
    {
        static int Main(string[] args)
        {
            return Cli.Execute(args, Console.Out) ? 0 : 1;
        }
    }
}
