using System;

namespace Dotnet.Shell.Logic.Console
{
    internal class HideCursor : IDisposable
    {
        private IConsole c;

        public HideCursor(IConsole c)
        {
            this.c = c;
            c.CursorVisible = false;
        }

        public void Dispose()
        {
            c.CursorVisible = true;
        }
    }
}
