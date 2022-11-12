using System;

namespace firebasic
{
    public static class Output
    {
        public static int Errors = 0, Warnings = 0;

        public static void Message(string file, int line, int column,
            string type, ConsoleColor col, string msg)
        {
            var old = Console.ForegroundColor;
            Console.Write($"{file}:{line}:{column}: ");
            Console.ForegroundColor = col;
            Console.Write($"{type}: ");
            Console.ForegroundColor = old;
            Console.WriteLine(msg);
        }

        public static void Error(string file, int line, int column, string msg)
        {
            Errors++;
            Message(file, line, column, "error", ConsoleColor.Red, msg);
        }

        public static void Warning(string file, int line, int column, string msg)
        {
            Warnings++;
            Message(file, line, column, "warning", ConsoleColor.Yellow, msg);
        }
    }
}
