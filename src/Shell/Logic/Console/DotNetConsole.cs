using Dotnet.Shell.Logic.Execution;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Dotnet.Shell.Logic.Console
{
    internal class DotNetConsole : IConsole
    {
        public int CursorLeft { get => System.Console.CursorLeft; set { System.Console.CursorLeft = value; } }
        public int CursorTop { get => System.Console.CursorTop; set { System.Console.CursorTop = value; } }

        public ConsoleColor ForegroundColor { get => System.Console.ForegroundColor; set { System.Console.ForegroundColor = value; } }
        public ConsoleColor BackgroundColor { get => System.Console.BackgroundColor; set { System.Console.BackgroundColor = value; } }

        bool IConsole.CursorVisible { set { System.Console.CursorVisible = value; } }
        int IConsole.WindowWidth { get => System.Console.WindowWidth; }
        int IConsole.WindowHeight { get => System.Console.WindowHeight; }

        public bool KeyAvailiable { get => System.Console.KeyAvailable; }

        public ConsoleKeyInfo ReadKey()
        {
            return System.Console.ReadKey();
        }

        public void Write(string text) => System.Console.Write(text);

        public void WriteLine(string message) => System.Console.WriteLine(message);

        public Task SaveAsync()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var p = OS.Exec("tput smcup"); // todo
                return p.WaitForExitAsync();
            }
            else
            {
                System.Console.Clear();
                return Task.CompletedTask;
            }
        }

        public Task RestoreAsync()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var p = OS.Exec("tput rmcup"); // todo
                return p.WaitForExitAsync();
            }
            else
            {
                System.Console.Clear();
                return Task.CompletedTask;
            }
        }
    }
}
