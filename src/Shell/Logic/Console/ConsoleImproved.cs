using Dotnet.Shell.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("UnitTests")]

namespace Dotnet.Shell.Logic.Console
{
    internal class ConsoleKeyEx : IEquatable<ConsoleKeyEx>
    {
        public ConsoleKey ?Key;
        public ConsoleModifiers Modifier = 0;

        public ConsoleKeyEx(ConsoleKey ?k, ConsoleModifiers m = 0)
        {
            this.Key = k;
            this.Modifier = m;
        }

        public static ConsoleKeyEx Any => new(null, 0);

        public override bool Equals(object y)
        {
            if (Key == null && Modifier == 0)
            {
                // Anything will match the Any key
                return true;
            }

            if (y is ConsoleKeyEx)
            {
                var yObj = y as ConsoleKeyEx;

                if (yObj == null && Modifier == 0)
                {
                    return true;
                }
                else
                {
                    return (this.Key == yObj.Key && this.Modifier == yObj.Modifier);
                }
            }
            return false;
        }

        public bool Equals([AllowNull] ConsoleKeyEx other)
        {
            return this.Equals(other as object);
        }

        public override int GetHashCode()
        {
            return (Key.ToString() + Modifier.ToString()).GetHashCode();
        }
    }

    internal class ConsoleImproved
    {
        public int CursorPosition = 0;
        public StringBuilder UserEnteredText { get; private set; } = new StringBuilder();
        public List<KeyValuePair<ConsoleKeyEx, Func<ConsoleImproved, ConsoleKeyEx, Task<bool>>>> KeyOverrides = new();
        public int UserEnteredTextPosition
        {
            get
            {
                var ret = 0;

                if (currentLineIndex != 0)
                {
                    ret += (Width - 1) - LastPromptLength; // first line
                    for (int x = 0; x < currentLineIndex - 1; x++) // for each line apart from the last
                    {
                        ret += Width - 1;
                    }

                    ret += CursorPosition; // for the last line just use the cursorpos
                }
                else
                {
                    ret = CursorPosition - LastPromptLength;
                }

#if DEBUG
                if (ret < 0)
                {
                    throw new InvalidOperationException("UserEnteredTextPosition is negative");
                }
#endif

                return ret;
            }
        }
        public Dotnet.Shell.API.Shell Shell { get; private set; }
        public int Width => implementation.WindowWidth;

        internal object Tag = null;
        internal int LastPromptLength = 0;

        private readonly IConsole implementation;
        private int CurrentHistoryIndex;
        private int LastRawPosition;
        private ConsoleKeyInfo LastKeyRead;

        private int totalExtraLinesCreated;
        private int currentLineIndex = 0;
        private int lastPromptCursorTop = 0;

        public ConsoleImproved(IConsole consoleImplementation, Dotnet.Shell.API.Shell shell)
        {
            this.implementation = consoleImplementation;
            this.Shell = shell;
            this.CurrentHistoryIndex = shell.History.Count != 0 ? shell.History.Count - 1 : 0;

            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Backspace), OnBackSpaceAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Delete), OnDeleteAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.LeftArrow), OnArrowAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.RightArrow), OnArrowAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.UpArrow), OnHistoryAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.DownArrow), OnHistoryAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Home), OnHomeAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.End), OnEndAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Enter), OnEnterAsync);
        }

        public void AddKeyOverride(ConsoleKeyEx key, Func<ConsoleImproved, ConsoleKeyEx, Task<bool>> func)
        {
            KeyOverrides.Add(new KeyValuePair<ConsoleKeyEx, Func<ConsoleImproved, ConsoleKeyEx, Task<bool>>>(key, func));
        }

        public void WriteLine(string message = "")
        {
            implementation.CursorLeft = CursorPosition;
            implementation.WriteLine(message);
            CursorPosition = 0;
        }

        public void WriteLine(ColorString text)
        {
            implementation.CursorLeft = CursorPosition;
            CursorPosition += text.Length;

            implementation.WriteLine(text.TextWithFormattingCharacters);
        }

        public void Write(string text = "", bool wrapText = false)
        {
            // remove any multiline padding characters
            //text = text.Replace("\0", "");

            if (wrapText)
            {
                var lastLineLength = 0;
                while (text.Length != 0)
                {
                    // amount to write on this line
                    var maxTextToWrite = Math.Min(text.Length, implementation.WindowWidth - CursorPosition - 1);

                    implementation.WriteLine(text.Substring(0, maxTextToWrite));
                    text = text.Remove(0, maxTextToWrite);
                    
                    CursorPosition = 0;
                    lastLineLength = maxTextToWrite;
                }
                CursorPosition = lastLineLength;
            }
            else
            {
                // only write upto the window edge if wrapText == false
                var maxTextToWrite = Math.Min(text.Length, implementation.WindowWidth - CursorPosition - 1);
                implementation.CursorLeft = CursorPosition;
                CursorPosition += maxTextToWrite;

                implementation.Write(text.Length != maxTextToWrite ? text.Substring(0, maxTextToWrite) : text);
            }          
        }

        public void Write(ColorString text)
        {
            implementation.CursorLeft = CursorPosition;
            bool useCursorLeft = implementation.CursorLeft == 0;

            // write and remove any multiline padding characters
            implementation.Write(text.TextWithFormattingCharacters/*.Replace("\0", "")*/);

            CursorPosition += text.Length;

            if (useCursorLeft)
            {
                CursorPosition = implementation.CursorLeft;
            }
            else
            {
                CursorPosition += text.Length;
            }
        }

        private int CalculateTotalNumberOfLines()
        {
            int extraLinesCreated = 0;

            var totalText = UserEnteredText.Length;
            // remove the first line which has the prompt
            totalText -= (Width - 1) - LastPromptLength;

            while (totalText > 0)
            {
                totalText -= Width - 1;
                extraLinesCreated++;
            }

            return extraLinesCreated;
        }

        private async Task<ConsoleKeyEx> ReadKeyAsync(bool addToInternalBuffer = true, CancellationToken token = default)
        {
            LastRawPosition = implementation.CursorLeft;

            var key = await Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (implementation.KeyAvailiable)
                        {
                            return implementation.ReadKey();
                        }
                        await Task.Delay(10);
                    }
                    catch
                    {

                    }
                }

                throw new TaskCanceledException();
            }, token);

            LastKeyRead = key;

            // increment pos if this is printable
            if (addToInternalBuffer && !char.IsControl(key.KeyChar) && !key.Modifiers.HasFlag(ConsoleModifiers.Control) && !key.Modifiers.HasFlag(ConsoleModifiers.Alt))
            {
                var oldTextPosition = UserEnteredTextPosition;

                using (new HideCursor(implementation))
                {
                    // if adding a character would take us over the edge of the console window we need to add a newline
                    if ((implementation.CursorLeft + 1) % implementation.WindowWidth == 0)
                    {
                        currentLineIndex++;
                        implementation.WriteLine(); // todo there may already be space?
                        implementation.CursorLeft = 0;
                        CursorPosition = 0;

                        if (oldTextPosition == UserEnteredText.Length)
                        {
                            // just append char
                            UserEnteredText.Append(key.KeyChar);
                        }
                        else
                        {
                            UserEnteredText = UserEnteredText.Insert(oldTextPosition, key.KeyChar);
                        }
                    }
                    else
                    {

                        // we now need to determine if we have to do a full redraw or can just add a character
                        if (oldTextPosition == UserEnteredText.Length)
                        {
                            // just append char
                            UserEnteredText.Append(key.KeyChar);
                        }
                        else
                        {
                            // insert at specified position
                            UserEnteredText = UserEnteredText.Insert(oldTextPosition, key.KeyChar);

                            // now clear everything typed in by the user
                            var oldPos = CursorPosition;
                            var oldTop = implementation.CursorTop;

                            ClearUserEntry();

                            // write from the start of the prompt
                            implementation.CursorTop = lastPromptCursorTop;
                            CursorPosition = LastPromptLength;
                            Write(UserEnteredText.ToString(), totalExtraLinesCreated != 0);

                            implementation.CursorTop = oldTop;
                            CursorPosition = oldPos;
                        }


                        CursorPosition++;
                        implementation.CursorLeft = CursorPosition;
                    }
                }
            }

            var numberOfExtraLines = CalculateTotalNumberOfLines();
            if (numberOfExtraLines > totalExtraLinesCreated)
            {
                totalExtraLinesCreated = numberOfExtraLines;
            }

            return new ConsoleKeyEx(key.Key, key.Modifiers);
        }

        internal async Task<string> GetCharAsync(CancellationToken cancellationToken = default)
        {
            var key = await ReadKeyAsync(false, cancellationToken);
            return key.Key.Value.ToString();
        }

        public async Task<string> GetCommandAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested) // read input from user loop
            {
                var keyEvent = await ReadKeyAsync(true, cancellationToken);

                bool stopFurtherProcessing = false;
                using (new HideCursor(implementation))
                {
                    foreach (var kvp in KeyOverrides.Where(x => x.Key.Equals(keyEvent)))
                    {
                        if (await kvp.Value(this, keyEvent))
                        {
                            // we've been requested to stop processing - that means no further handlers will be run
                            stopFurtherProcessing = true;
                            break;
                        }
                    }
                }

                if (!stopFurtherProcessing && keyEvent.Key == ConsoleKey.Enter)
                {
                    WriteLine();
                    break;
                }
            }

            // replace any padding characters with the null string
            var command = UserEnteredText.Replace("\\\0", "").Replace("\0", "").ToString();

            foreach (var handler in Shell.CommandHandlers)
            {
                command = handler(command);
            }

            if (!string.IsNullOrWhiteSpace(command))
            {
                Shell.History.Add(new API.HistoryItem(command, DateTime.UtcNow));
                // This looks like an off but 1 but the history code is going to decremement this if you hit up
                CurrentHistoryIndex = Shell.History.Count;
            }

            return command;
        }

        public void DisplayPrompt()
        {
            // first we need to check if std out has put us in an odd location
            // if it has, by say cat-ing a file without a newline we want to add a newline
            // the behaviour is different from bash but looks so much better and stops the next
            // logic clobbering stdout

            if (implementation.CursorLeft != 0)
            {
                WriteLine();
            }

            implementation.CursorLeft = 0;
            CursorPosition = 0;
            totalExtraLinesCreated = 0;

            var prompt = Shell.Prompt();
            Write(prompt);

            LastPromptLength = implementation.CursorLeft;
            lastPromptCursorTop = implementation.CursorTop;
            
            UserEnteredText.Clear();
            ClearUserEntry();
            implementation.CursorLeft = LastPromptLength;

            totalExtraLinesCreated = 0;
            currentLineIndex = 0;
        }

        public void DisplayPrompt(string text)
        {
            DisplayPrompt();

            Write(text);
            UserEnteredText.Append(text);
        }

        public void ClearUserEntry(int pos = -1)
        {
            if (pos == -1 && Shell != null)
            {
                pos = LastPromptLength;
            }
            else if (pos == -1)
            {
                pos = 0;
            }
            else
            {
                pos += LastPromptLength;
            }

            if (pos > implementation.WindowWidth)
            {
                throw new NotImplementedException("Cannot write beyond window width...yet");
            }

            if (totalExtraLinesCreated != 0)
            {
                for (int x = 0; x <= totalExtraLinesCreated; x++)
                {
                    implementation.CursorTop = lastPromptCursorTop + x;
                    CursorPosition = x == 0 ? pos : 0;
                    var eraseWidth = x == 0 ? implementation.WindowWidth - pos - 1 : implementation.WindowWidth - 1;

                    Write(new string(' ', eraseWidth));
                }

                implementation.CursorLeft = pos;
                implementation.CursorTop = lastPromptCursorTop;
                CursorPosition = pos;
            }
            else
            {
                var oldPos = CursorPosition;
                CursorPosition = pos;
                Write(new string(' ', implementation.WindowWidth - pos - 1));
                CursorPosition = oldPos;
            }
        }

        public void ReplaceUserEntryAtPosition(string text, int userPos)
        {
            implementation.CursorVisible = false;
            ClearUserEntry(userPos);
            CursorPosition = LastPromptLength + userPos;
            Write(text);

            UserEnteredText = UserEnteredText.Remove(userPos, UserEnteredText.Length - userPos).Insert(userPos, text);

            LastRawPosition = implementation.CursorLeft;
            implementation.CursorVisible = true;
        }

        public void IgnoreTab()
        {
            if (LastKeyRead.KeyChar == '\t')
            {
                implementation.CursorLeft = LastRawPosition;
            }
        }

        private Task<bool> OnBackSpaceAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (UserEnteredTextPosition > 0)
                {
                    using (new HideCursor(implementation))
                    {
                        var originalCursorPos = CursorPosition;
                        var originalCursorTop = implementation.CursorTop;

                        UserEnteredText = UserEnteredText.Remove(UserEnteredTextPosition - 1, 1);
                        ClearUserEntry();

                        CursorPosition = LastPromptLength;
                        implementation.CursorLeft = CursorPosition;
                        Write(UserEnteredText.ToString(), totalExtraLinesCreated != 0);

                        if (originalCursorPos - 1 >= 0)
                        {
                            CursorPosition = originalCursorPos - 1;
                        }
                        else
                        {
                            // need to go up a line
                            CursorPosition = implementation.WindowWidth - 2;
                            originalCursorTop--;
                            currentLineIndex--;
                        }

                       
                        implementation.CursorLeft = CursorPosition;
                        implementation.CursorTop = originalCursorTop;
                    }
                }
                else
                {
                    implementation.CursorLeft = LastPromptLength;
                }

                return false;
            });
        }

        private Task<bool> OnDeleteAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (UserEnteredTextPosition < UserEnteredText.Length)
                {
                    implementation.CursorVisible = false;
                    var originalCursorPos = CursorPosition;

                    UserEnteredText = UserEnteredText.Remove(UserEnteredTextPosition, 1);
                    ClearUserEntry();
                    CursorPosition = LastPromptLength;
                    Write(UserEnteredText.ToString());

                    CursorPosition = originalCursorPos;
                    implementation.CursorLeft = CursorPosition;

                    implementation.CursorVisible = true;
                }

                return false;
            });
        }

        private Task<bool> OnHomeAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() => { 
                CursorPosition = LastPromptLength;
                currentLineIndex = 0;

                implementation.CursorLeft = CursorPosition;
                implementation.CursorTop = lastPromptCursorTop;

                return false;
            });
        }

        private Task<bool> OnEndAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() => {

                if (totalExtraLinesCreated > 0)
                {
                    currentLineIndex = totalExtraLinesCreated;
                    implementation.CursorTop = lastPromptCursorTop + UserEnteredText.Length / (implementation.WindowWidth - 1);

                    CursorPosition = UserEnteredText.Length;
                    // remove the first line which has the prompt
                    CursorPosition -= (Width - 1) - LastPromptLength;

                    for (int x = 0; x < currentLineIndex - 1; x++) // for each line apart from the first
                    {
                        CursorPosition -= Width - 1;
                    }

                    implementation.CursorLeft = CursorPosition;
                }
                else
                {
                    CursorPosition = LastPromptLength + UserEnteredText.Length;
                    implementation.CursorLeft = CursorPosition;
                }

                return false;
            });
        }

        private Task<bool> OnArrowAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() => {

                if (key.Key == ConsoleKey.LeftArrow)
                {
                    if (currentLineIndex == 0 && CursorPosition - 1 >= LastPromptLength || // single line
                        currentLineIndex > 0 && CursorPosition - 1 >= 0) // multiline case
                    {
                        CursorPosition--;
                    }
                    else if (currentLineIndex > 0)
                    {
                        // we need to move up a line and we are at the edge
                        currentLineIndex--;
                        implementation.CursorTop--;
                        CursorPosition = implementation.WindowWidth - 2; // -1 for edge, -1 to move cursor back 1
                    }

                    implementation.CursorLeft = CursorPosition;
                }
                else
                {
                    if (UserEnteredTextPosition + 1 <= UserEnteredText.Length && implementation.CursorLeft + 1 <= implementation.WindowWidth -1)
                    {
                        // can advance in string and position would not go over the window edge
                        CursorPosition++;
                    }
                    else if (UserEnteredTextPosition + 1 <= UserEnteredText.Length && implementation.CursorLeft + 1 > implementation.WindowWidth - 1)
                    {
                        // would go over the edge of the window
                        CursorPosition = 0;
                        currentLineIndex++;
                        implementation.CursorTop++;
                    }

                    implementation.CursorLeft = CursorPosition;
                }

                return false;
            });
        }

        private Task<bool> OnHistoryAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (key.Key == ConsoleKey.UpArrow)
                {
                    CurrentHistoryIndex--;
                    if (CurrentHistoryIndex < 0)
                    {
                        CurrentHistoryIndex = 0;
                    }
                }
                else
                {
                    CurrentHistoryIndex++;
                    if (CurrentHistoryIndex >= Shell.History.Count)
                    {
                        CurrentHistoryIndex = Shell.History.Count - 1;
                    }
                }

                if (CurrentHistoryIndex < Shell.History.Count)
                {
                    ClearUserEntry();
                    CursorPosition = LastPromptLength;
                    implementation.CursorLeft = LastPromptLength;

                    var newCmd = Shell.History[CurrentHistoryIndex].CmdLine;

                    Write(newCmd, newCmd.Length + LastPromptLength > Width);
                    UserEnteredText.Clear();
                    UserEnteredText.Append(newCmd);
                }

                return false;
            });
        }

        private Task<bool> OnEnterAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                // as in Bash escaping can only be performed at the end of the line
                // ie you cant just jump to multiline in the middle of an existing command
                if (UserEnteredText.Length != 0 && UserEnteredTextPosition == UserEnteredText.Length && UserEnteredText[^1] == '\\')
                {
                    // remove the \ character
                    //UserEnteredText.Remove(UserEnteredText.Length -1, 1);

                    // pad out the line with \0

                    // Can't use CursorLeft as its already on a newline - use lastRawPosition
                    // we can use that value as we are being executed write after it is set
                    var charsToAdd = Width - LastRawPosition -1;
                    UserEnteredText = UserEnteredText.Append(new string('\0', charsToAdd));

                    // force a newline
                    totalExtraLinesCreated++;
                    currentLineIndex++;
                    implementation.CursorLeft = 0;
                    CursorPosition = 0;
                    implementation.WriteLine(); // todo there may already be space?
                    return true;
                }
                return false;
            });
        }
    }
}
