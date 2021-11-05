using Dotnet.Shell.API;
using Dotnet.Shell.Logic.Console;
using Dotnet.Shell.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
                ConsoleImproved console = new(new MockConsole(), fakeShell);
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
                console.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Tab), (a, b) => { handlerCalled++; return Task<bool>.FromResult(false); });

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
                var fakeShell = new Shell
                {
                    Prompt = () => { return Prompt; }
                };

                var mockConsole = new MockConsole();
                var console = new ConsoleImproved(mockConsole, fakeShell);

                console.DisplayPrompt();

                Assert.IsTrue(mockConsole.Output.ToString().StartsWith(Prompt));
            }
        }

        [TestMethod]
        public void ConsoleTextSpanWrapping()
        {
            const string str = "abcdefghijklmnopqrstuvwxyz";

            ConsoleTextSpan cts = new ConsoleTextSpan();
            cts.Append(str);
            cts.Insert(0, '0', 4, 5);

            const string expected = @"0
abcde
fghij
klmno
pqrst
uvwxy
z";
            Assert.AreEqual(expected.Replace("\r", string.Empty), cts.ToString());


            const string input = @"abcdefgh
ijklmnopqrst
12 ggg";

            cts = new ConsoleTextSpan();
            cts.Append(input.Replace("\r", string.Empty));
            cts.Insert(3, 'x', 4, 12);

            const string output = @"abcxdefg
hijklmnopqrs
t12 ggg";
            Assert.AreEqual(output.Replace("\r", string.Empty), cts.ToString());
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

                        // count \n
                        var newlines = console.UserEnteredText.ToString().Where(x => x == '\n').Count();
                        Assert.AreEqual(lastPos + newlines, console.UserEnteredTextPosition);
                    }
                    return Task<bool>.FromResult(false); 
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
                fakeShell.CommandHandlers.Add((cmd) => { Assert.AreEqual("y"+input+"x", cmd); return cmd; });
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

                for (int x = 0; x < input.Length; x++)
                {
                    mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.LeftArrow));
                }
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Y));

                for (int x = 0; x < input.Length; x++)
                {
                    mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.RightArrow));
                }

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.X));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                _ = await console.GetCommandAsync(CancellationToken.None);
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

        [TestMethod]
        public async Task PreviousNextWordAsync()
        {
            using (var ms = new MemoryStream())
            {
                var input = "aaa bbb ccc ddd eee xxx fff ggg yyy";

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

                    if (character != ConsoleKey.G)
                    {
                        mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Spacebar));
                    }
                }

                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.LeftArrow, ConsoleModifiers.Control));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.LeftArrow, ConsoleModifiers.Control));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.X));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.X));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.X));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Spacebar));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.RightArrow, ConsoleModifiers.Control));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.RightArrow, ConsoleModifiers.Control));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Spacebar));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Y));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Y));
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Y));
                // aaa bbb ccc ddd eee xxx fff ggg yyy
                mockConsole.keys.Enqueue(new ConsoleKeyEx(ConsoleKey.Enter));

                var command = await console.GetCommandAsync(CancellationToken.None);

                Assert.AreEqual(input, command);
            }
        }
    }

    internal class MockConsole : IConsole
    {
        public StringBuilder Output = new();
        public Queue<ConsoleKeyEx> keys = new();

        private int savedCursorLeft = 0;
        private int savedCursorTop = 0;
        private int cursorLeft = 0;
        private int cursorTop = 0;

        public int CursorLeft { 
            get { return cursorLeft; } 
            set { Assert.IsTrue(value >= 0); cursorLeft = value; } }

        public int CursorTop { 
            get { return cursorTop + 1; } 
            set {
                value -= 1;

                Assert.IsTrue(value >= 0);
                var output = Output.ToString();

                var lines = Output.ToString().Split('\n').Count();

                if (value >= lines)
                {

                }

                Assert.IsTrue(value < lines);

                cursorTop = value; 
            } 
        }

        public bool CursorVisible { set; get; } = true;

        public int WindowWidth { set; get; } = 100;

        public int WindowHeight { set; get; } = 100;

        public ConsoleColor ForegroundColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ConsoleColor BackgroundColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool KeyAvailiable => keys.Count != 0;

        public void ClearCurrentLine(int pos = -1)
        {
            var lines = Output.ToString().Split('\n');

            if (pos == -1)
            {
                lines[cursorTop] = string.Empty;
            }
            else
            {
                if (pos < lines[cursorTop].Length)
                {
                    lines[cursorTop] = lines[cursorTop].Remove(pos);
                }               
            }

            Output = new StringBuilder(string.Join('\n', lines));
        }

        public void MoveCursorDown(int lines)
        {
            CursorTop += lines;
        }

        public void MoveCursorUp(int lines)
        {
            CursorTop -= lines;
        }

        public ConsoleKeyInfo ReadKey()
        {
            var keyEx = keys.Dequeue();

            char final;
            if (keyEx.Key.Value == ConsoleKey.Tab)
            {
                final = '\t';
            }
            else if (keyEx.Key.Value == ConsoleKey.Spacebar)
            {
                final = ' ';
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
            return new ConsoleKeyInfo(final, keyEx.Key.Value, keyEx.Modifier == ConsoleModifiers.Shift, keyEx.Modifier == ConsoleModifiers.Alt, keyEx.Modifier == ConsoleModifiers.Control);
        }

        public Task RestoreAsync()
        {
            throw new NotImplementedException();
        }

        public void RestoreCursorPosition(Action onRestore = null)
        {
            cursorTop = savedCursorTop;
            cursorLeft = savedCursorLeft;
            onRestore?.Invoke();
        }

        public void SaveCursorPosition()
        {
            savedCursorTop = cursorTop;
            savedCursorLeft = cursorLeft;
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
            Output.Append(message + "\n");
            CursorTop++;
        }
    }
}
