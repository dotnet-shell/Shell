using Dotnet.Shell.API;
using Dotnet.Shell.Logic.Console;
using Dotnet.Shell.Logic.Suggestions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class SuggestionsTests
    {
        [TestMethod]
        public void Construction()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                Suggestions s = new(fakeShell);
            } 
        }

        [TestMethod]
        public void Registration()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                Suggestions s = new(fakeShell);

                ConsoleImproved console = new(new MockConsole(), fakeShell);
                console.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Tab), s.OnTabSuggestCmdAsync);
            }
        }

        [TestMethod]
        public async Task SuggestCSharpMultipleEntriesAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                Suggestions s = new(fakeShell);

                var mockConsole = new MockConsole();

                ConsoleImproved console = new(mockConsole, fakeShell);
                console.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Tab), s.OnTabSuggestCmdAsync);

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.C, ConsoleModifiers.Shift));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.O));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.N));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.S));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.O));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.L));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.E));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.OemPeriod));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Tab));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Tab));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var command = await console.GetCommandAsync(CancellationToken.None);

                Assert.AreEqual("Console.", command);

                var stdOut = mockConsole.Output.ToString();

                Assert.IsTrue(stdOut.Contains("WriteLine"));
                Assert.IsTrue(stdOut.Contains("Beep"));
            }
        }

        [TestMethod]
        public async Task SuggestCSharpSingleEntryAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                Suggestions s = new(fakeShell);

                var mockConsole = new MockConsole();

                ConsoleImproved console = new(mockConsole, fakeShell);
                console.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Tab), s.OnTabSuggestCmdAsync);

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.C, ConsoleModifiers.Shift));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.O));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.N));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.S));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.O));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.L));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.E));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.OemPeriod));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.B, ConsoleModifiers.Shift));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.E));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.E));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Tab));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var command = await console.GetCommandAsync(CancellationToken.None);

                Assert.AreEqual("Console.Beep", command);

                var stdOut = mockConsole.Output.ToString();
            }
        }
    }
}
