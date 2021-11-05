using Dotnet.Shell.Logic.Console;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace UnitTests
{
    class AssertingConsole : IConsole
    {
        public int CursorLeft { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int CursorTop { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool CursorVisible { set => throw new NotImplementedException(); }

        public int WindowWidth => throw new NotImplementedException();

        public int WindowHeight => throw new NotImplementedException();

        public ConsoleColor ForegroundColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ConsoleColor BackgroundColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool KeyAvailiable => throw new NotImplementedException();

        public void ClearCurrentLine(int pos = -1)
        {
            throw new NotImplementedException();
        }

        public void MoveCursorDown(int lines)
        {
            throw new NotImplementedException();
        }

        public void MoveCursorUp(int lines)
        {
            throw new NotImplementedException();
        }

        public ConsoleKeyInfo ReadKey()
        {
            Assert.Fail();
            throw new Exception();
        }

        public Task RestoreAsync()
        {
            Assert.Fail();
            throw new Exception();
        }

        public void RestoreCursorPosition(Action onRestore = null)
        {
            throw new NotImplementedException();
        }

        public Task SaveAsync()
        {
            Assert.Fail();
            throw new Exception();
        }

        public void SaveCursorPosition()
        {
            throw new NotImplementedException();
        }

        public void Write(string text = null)
        {
            Assert.Fail();
        }

        public void WriteLine(string message = null)
        {
            Assert.Fail();
        }
    }
}
