using Dotnet.Shell.Logic.Execution;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.Shell.Logic.Console
{
    public class DotNetConsole : IConsole
    {
        public int CursorLeft { get => System.Console.CursorLeft; set { System.Console.CursorLeft = value; } }
        public int CursorTop { get => System.Console.CursorTop; set { System.Console.CursorTop = value; } }

        public ConsoleColor ForegroundColor { get => System.Console.ForegroundColor; set { System.Console.ForegroundColor = value; } }
        public ConsoleColor BackgroundColor { get => System.Console.BackgroundColor; set { System.Console.BackgroundColor = value; } }

        bool IConsole.CursorVisible { set { System.Console.CursorVisible = value; } }
        int IConsole.WindowWidth { get => System.Console.WindowWidth; }
        int IConsole.WindowHeight { get => System.Console.WindowHeight; }

        public bool KeyAvailiable { get => System.Console.KeyAvailable; }

        private Point savedCursorPos;

        public ConsoleKeyInfo ReadKey()
        {
            return System.Console.ReadKey();
        }

        public void Write(string text) => System.Console.Write(text);

        public void WriteLine(string message) => System.Console.WriteLine(message);

        public Task SaveAsync()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

        public void ClearCurrentLine(int pos = -1)
        {
            if (pos == -1) // clear entire line, don't change position
            {
                // If n is 2, clear entire line. Cursor position does not change.
                System.Console.Write("\u001B[2K");
            }
            else // clear from position which will be set
            {
                //If n is 0 (or missing), clear from cursor to the end of the line.
                System.Console.Write("\u001B[0K");
            }
        }

        public void SaveCursorPosition()
        {
            savedCursorPos = new Point(System.Console.CursorLeft, System.Console.CursorTop);
        }

        public void RestoreCursorPosition(Action onRestore = null)
        {
            System.Console.CursorLeft = savedCursorPos.X;
            System.Console.CursorTop = savedCursorPos.Y;
            onRestore?.Invoke();
        }

        public void MoveCursorDown(int lines)
        {
            System.Console.Write("\u001B[" + lines + "B");
        }

        public void MoveCursorUp(int lines)
        {
            System.Console.Write("\u001B[" + lines + "A");
        }
    }
}
