using System;

namespace AzureIoTEdge.MiddlewareModule
{
    public static class Logger
    {
        public static void Log(string text)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] {text}");
        }

        public static void Error(string text)
        {            
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] {text}");
            Console.ForegroundColor = prevColor;
        }

        public static void Error(string text, Exception ex)
        {            
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] {text}\n{ex.ToString()}");
            Console.ForegroundColor = prevColor;
        }
    }
}