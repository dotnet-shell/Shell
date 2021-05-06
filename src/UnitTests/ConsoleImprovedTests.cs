using Dotnet.Shell.API;
using Dotnet.Shell.Logic.Console;
using Dotnet.Shell.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class ConsoleImprovedTests
    {
        [TestMethod]
        public void Construct()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                ConsoleImproved console = new ConsoleImproved(new MockConsole(), fakeShell);
            }
        }

        [TestMethod]
        public async Task AddKeyOverrideAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var mockConsole = new MockConsole();
                var console = new ConsoleImproved(mockConsole, fakeShell);

                int handlerCalled = 0;
                console.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Tab), (a, b) => { handlerCalled++; return Task.CompletedTask; });

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.A));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Tab));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var key = await console.GetCommandAsync(CancellationToken.None);

                Assert.AreEqual("a", key);
                Assert.AreEqual(1, handlerCalled);
            }
        }

        [TestMethod]
        public void Write()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var mockConsole = new MockConsole();
                var console = new ConsoleImproved(mockConsole, fakeShell);

                console.Write("hello");
                console.WriteLine("world");

                Assert.AreEqual("helloworld\n", mockConsole.Output.ToString());
            }
        }

        [TestMethod]
        public void Prompt()
        {
            const string Prompt = "test>";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.Prompt = () => { return Prompt; };

                var mockConsole = new MockConsole();
                var console = new ConsoleImproved(mockConsole, fakeShell);

                console.DisplayPrompt();

                Assert.IsTrue(mockConsole.Output.ToString().StartsWith(Prompt));
            }
        }

        [TestMethod]
        public async Task CommandOverrideAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.CommandHandlers.Add((cmd) => { Assert.AreEqual("aaaaa", cmd); return "TEST"; });

                var mockConsole = new MockConsole();
                var console = new ConsoleImproved(mockConsole, fakeShell);

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.A));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.A));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.A));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.A));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.A));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var command = await console.GetCommandAsync(CancellationToken.None);

                Assert.AreEqual("TEST", command);
            }
        }

        [TestMethod]
        public async Task MultiLineInputAsync()
        {
            using (var ms = new MemoryStream())
            {
                var input = "aaabbbcccdddeeefffggg";

                var fakeShell = new Shell();
                fakeShell.CommandHandlers.Add((cmd) => { Assert.AreEqual(input, cmd); return cmd; });
                fakeShell.Prompt = () => { return new ColorString("# > ", "# > "); };

                var mockConsole = new MockConsole()
                {
                    WindowWidth = 12,
                };
                var console = new ConsoleImproved(mockConsole, fakeShell);

                var lastPos = 0;
                var stringInProgress = string.Empty;
                console.AddKeyOverride(ConsoleKeyEx.Any, (console, key) => 
                {
                    if (key.Key != ConsoleKey.Enter)
                    {
                        lastPos++;
                        // This makes sure that after every character the position variable is correctly incremented by 1
                        Assert.AreEqual(console.UserEnteredText.Length, console.UserEnteredTextPosition);
                        Assert.AreEqual(lastPos, console.UserEnteredTextPosition);
                    }
                    return Task.CompletedTask; 
                });

                console.DisplayPrompt();

                foreach (var character in new[] { ConsoleKey.A, ConsoleKey.B, ConsoleKey.C, ConsoleKey.D, ConsoleKey.E, ConsoleKey.F, ConsoleKey.G })
                {
                    for (int x = 0; x < 3; x++)
                    {
                        mockConsole.keys.Enqueue(new ConsoleKeyEx(character));
                    }
                }
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var command = await console.GetCommandAsync(CancellationToken.None);

                Assert.AreEqual(input, command);
            }
        }

        [TestMethod]
        public async Task MultiLineArrowKeysAsync()
        {
            using (var ms = new MemoryStream())
            {
                var input = "aaabbbcccdddeeefffggg";

                var fakeShell = new Shell();
                fakeShell.CommandHandlers.Add((cmd) => { Assert.AreEqual(input, cmd); return cmd; });
                fakeShell.Prompt = () => { return new ColorString("# > ", "# > "); };

                var mockConsole = new MockConsole()
                {
                    WindowWidth = 12,
                };
                var console = new ConsoleImproved(mockConsole, fakeShell);

                int pos = 0;
                console.AddKeyOverride(ConsoleKeyEx.Any, (console, key) =>
                {
                    if (key.Key == ConsoleKey.LeftArrow)
                    {
                        pos--;
                    }
                    else if (key.Key == ConsoleKey.RightArrow || key.Key != ConsoleKey.Enter)
                    {
                        pos++;
                    }
                    Assert.AreEqual(pos, console.UserEnteredTextPosition);
                    return Task.CompletedTask;
                });

                console.DisplayPrompt();

                foreach (var character in new[] { ConsoleKey.A, ConsoleKey.B, ConsoleKey.C, ConsoleKey.D, ConsoleKey.E, ConsoleKey.F, ConsoleKey.G })
                {
                    for (int x = 0; x < 3; x++)
                    {
                        mockConsole.keys.Enqueue(new ConsoleKeyEx(character));
                    }
                }

                for (int x = 0; x < input.Length; x++)
                {
                    mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.LeftArrow));
                }

                for (int x = 0; x < input.Length; x++)
                {
                    mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.RightArrow));
                }

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var command = await console.GetCommandAsync(CancellationToken.None);

            }
        }

        [TestMethod]
        public async Task MultiLineBackspaceAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.CommandHandlers.Add((cmd) => { Assert.IsTrue(string.IsNullOrWhiteSpace(cmd)); return cmd; });
                fakeShell.Prompt = () => { return new ColorString("# > ", "# > "); };

                var mockConsole = new MockConsole()
                {
                    WindowWidth = 12,
                };
                var console = new ConsoleImproved(mockConsole, fakeShell);

                int lastPos = 0;
                console.AddKeyOverride(ConsoleKeyEx.Any, (console, key) =>
                {
                    if (key.Key == ConsoleKey.Backspace)
                    {
                        // This makes sure that after every character the position variable is correctly incremented by 1
                        lastPos--;
                        Assert.AreEqual(console.UserEnteredText.Length, console.UserEnteredTextPosition);
                        Assert.AreEqual(lastPos, console.UserEnteredTextPosition);
                    }
                    else if (key.Key != ConsoleKey.Enter)
                    {
                        lastPos++;
                    }
                    return Task.CompletedTask;
                });

                console.DisplayPrompt();

                foreach (var character in new[] { ConsoleKey.A, ConsoleKey.B, ConsoleKey.C, ConsoleKey.D, ConsoleKey.E, ConsoleKey.F, ConsoleKey.G })
                {
                    for (int x = 0; x < 3; x++)
                    {
                        mockConsole.keys.Enqueue(new ConsoleKeyEx(character));
                    }
                }

                foreach (var character in new[] { ConsoleKey.A, ConsoleKey.B, ConsoleKey.C, ConsoleKey.D, ConsoleKey.E, ConsoleKey.F, ConsoleKey.G })
                {
                    for (int x = 0; x < 3; x++)
                    {
                        mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Backspace));
                    }
                }

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var command = await console.GetCommandAsync(CancellationToken.None);

                Assert.IsTrue(string.IsNullOrWhiteSpace(command));
            }
        }

        [TestMethod]
        public async Task MultiLineHomeEndInputAsync()
        {
            using (var ms = new MemoryStream())
            {
                var input = "xxaaabbbcccdddeeefffgggz";

                var fakeShell = new Shell();
                fakeShell.CommandHandlers.Add((cmd) => { Assert.AreEqual(input, cmd); return cmd; });
                fakeShell.Prompt = () => { return new ColorString("# > ", "# > "); };

                var mockConsole = new MockConsole()
                {
                    WindowWidth = 12,
                };
                var console = new ConsoleImproved(mockConsole, fakeShell);

                console.DisplayPrompt();

                foreach (var character in new[] { ConsoleKey.A, ConsoleKey.B, ConsoleKey.C, ConsoleKey.D, ConsoleKey.E, ConsoleKey.F, ConsoleKey.G })
                {
                    for (int x = 0; x < 3; x++)
                    {
                        mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Home));
                        mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.End));
                        mockConsole.keys.Enqueue(new ConsoleKeyEx(character));
                    }
                }

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Home));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.X));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.End));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Z));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Home));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.X));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var command = await console.GetCommandAsync(CancellationToken.None);

                Assert.AreEqual(input, command);
            }
        }

        [TestMethod]
        public async Task MultiLineInsertAsync()
        {
            using (var ms = new MemoryStream())
            {
                var input = "aaabbbcccxxxdddyyyeeefffggg";

                var fakeShell = new Shell();
                fakeShell.CommandHandlers.Add((cmd) => { Assert.AreEqual(input, cmd); return cmd; });
                fakeShell.Prompt = () => { return new ColorString("# > ", "# > "); };

                var mockConsole = new MockConsole()
                {
                    WindowWidth = 12,
                };
                var console = new ConsoleImproved(mockConsole, fakeShell);

                console.DisplayPrompt();

                foreach (var character in new[] { ConsoleKey.A, ConsoleKey.B, ConsoleKey.C, ConsoleKey.D, ConsoleKey.E, ConsoleKey.F, ConsoleKey.G })
                {
                    for (int x = 0; x < 3; x++)
                    {
                        mockConsole.keys.Enqueue(new ConsoleKeyEx(character));
                    }
                }

                //aaabbbcccdddeeefffggg
                for (int x = 0; x < 12; x++)
                {
                    mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.LeftArrow));
                }
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.X));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.X));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.X));

                for (int x = 0; x < 3; x++)
                {
                    mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.RightArrow));
                }
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Y));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Y));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Y));

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var command = await console.GetCommandAsync(CancellationToken.None);

                Assert.AreEqual(input, command);
            }
        }
    }

    internal class MockConsole : IConsole
    {
        public StringBuilder Output = new StringBuilder();
        public Queue<ConsoleKeyEx> keys = new Queue<ConsoleKeyEx>();

        private int cursorLeft = 0;
        private int cursorTop = 0;

        public int CursorLeft { get { return cursorLeft; } set { Assert.IsTrue(value >= 0); cursorLeft = value; } }

        public int CursorTop { get { return cursorTop; } set { Assert.IsTrue(value >= 0); cursorTop = value; } }

        public bool CursorVisible { set; get; } = true;

        public int WindowWidth { set; get; } = 100;

        public int WindowHeight { set; get; } = 100;

        public ConsoleColor ForegroundColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ConsoleColor BackgroundColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool KeyAvailiable => keys.Count != 0;

        public ConsoleKeyInfo ReadKey()
        {
            var keyEx = keys.Dequeue();

            char final = '\r';
            if (keyEx.Key.Value == ConsoleKey.Tab)
            {
                final = '\t';
            }
            else if (keyEx.Key.Value == ConsoleKey.Enter)
            {
                final = '\r';
            }
            else if (keyEx.Key.Value == ConsoleKey.Backspace || 
                     keyEx.Key.Value == ConsoleKey.Home ||
                     keyEx.Key.Value == ConsoleKey.End ||
                     keyEx.Key.Value == ConsoleKey.LeftArrow ||
                     keyEx.Key.Value == ConsoleKey.RightArrow)
            {
                final = '\0';
            }
            else if (keyEx.Key.Value == ConsoleKey.OemPeriod)
            {
                final = '.';
            }
            else
            {
                final = char.ToLower(keyEx.Key.Value.ToString()[0]);
                if (keyEx.Modifier == ConsoleModifiers.Shift)
                {
                    final = char.ToUpper(final);
                }
            }

            CursorLeft++;
            return new ConsoleKeyInfo(final, keyEx.Key.Value, false, false, false);
        }

        public Task RestoreAsync()
        {
            throw new NotImplementedException();
        }

        public Task SaveAsync()
        {
            throw new NotImplementedException();
        }

        public void Write(string text = null)
        {
            CursorLeft += text.Length;
            Output.Append(text);
        }

        public void WriteLine(string message = null)
        {
            CursorLeft += message == null ? 0 : message.Length;
            CursorTop++;
            Output.Append(message+"\n");
        }
    }
}
