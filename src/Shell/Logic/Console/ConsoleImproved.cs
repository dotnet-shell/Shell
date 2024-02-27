using Dotnet.Shell.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dotnet.Shell.Logic.Console
{
    /// <summary>
    /// A class that wraps both ConsoleKey and ConsoleModifiers to provide a clear
    /// representation of a keypress
    /// </summary>
    /// <seealso cref="System.IEquatable&lt;Dotnet.Shell.Logic.Console.ConsoleKeyEx&gt;" />
    public class ConsoleKeyEx : IEquatable<ConsoleKeyEx>
    {
        /// <summary>
        /// Gets the key.
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        public ConsoleKey? Key { get; }

        /// <summary>
        /// Gets the modifier.
        /// </summary>
        /// <value>
        /// The modifier.
        /// </value>
        public ConsoleModifiers Modifier { get; }  = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleKeyEx"/> class.
        /// </summary>
        /// <param name="k">The k.</param>
        /// <param name="m">The m.</param>
        public ConsoleKeyEx(ConsoleKey? k, ConsoleModifiers m = 0)
        {
            Key = k;
            Modifier = m;
        }

        /// <summary>
        /// Gets an object representing ANY keypress
        /// </summary>
        /// <value>
        /// Any keypress object
        /// </value>
        public static ConsoleKeyEx Any => new ConsoleKeyEx(null, 0);

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="y">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
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
                    return (Key == yObj.Key && Modifier == yObj.Modifier);
                }
            }
            return false;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        ///   <see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.
        /// </returns>
        public bool Equals([AllowNull] ConsoleKeyEx other)
        {
            return Equals(other as object);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return (Key.ToString() + Modifier.ToString()).GetHashCode();
        }
    }

    internal class ConsoleTextSpan
    {
        private StringBuilder userText = new StringBuilder();

        public char this[int i]
        {
            get { return userText[i]; }
        }

        public int GetLogicCursorPosition(int currentLineIndex, int xPos, int lengthOfPrompt)
        {
            var ret = 0;

            if (userText.Length == 0)
            {
                return ret;
            }

            if (currentLineIndex < 0)
            {
                throw new InvalidOperationException("currentLineIndex is invalid");
            }

            if (xPos < 0)
            {
                throw new InvalidOperationException("xPos is invalid");
            }

            if (currentLineIndex != 0 && currentLineIndex > Lines)
            {
                throw new InvalidOperationException("currentLineIndex is invalid: > total lines");
            }

            if (currentLineIndex == 0 || Lines == 0)
            {
                ret = xPos - lengthOfPrompt;
            }
            else
            {
                // multiline
                var lines = userText.ToString().Split('\n');

                // sum the length of lines under the one we want
                for (int x = 0; x < currentLineIndex; x++)
                {
                    ret += lines[x].Length + 1;
                }

                ret += xPos;
            }

#if DEBUG
            if (ret < 0)
            {
                throw new InvalidOperationException("userTextPosition is negative");
            }

            if (ret > userText.Length)
            {
                throw new InvalidOperationException("userTextPosition is out of bounds");
            }
#endif

            return ret;
        }

        private int CalculateTotalNumberOfLines()
        {
            int totalLines = 0;

            for (int x = 0; x < userText.Length; x++)
            {
                if (userText[x] == '\n')
                {
                    totalLines++;
                }
            }

            return totalLines;
        }

        public void Append(string s) => userText.Append(s);

        public void Append(char s) => userText.Append(s);

        public void Insert(int pos, string s)
        {
            userText = userText.Insert(pos, s);
        }

        public void Insert(int pos, char s, int promptLength, int lineWidth)
        {
            userText = userText.Insert(pos, s);

            ReflowText(promptLength, lineWidth);
        }

        private void ReflowText(int promptLength, int lineWidth)
        {
            // replace \<\n> with \0 to preserve, remove any newlines
            var tempText = userText.ToString().Replace("\\\n", "\0").Replace("\n", string.Empty);

            var baseString = tempText.ToString();
            var firstLine = new string(baseString.Take(lineWidth - promptLength).ToArray());

            userText.Clear();
            userText.Append(firstLine);

            // only add \n if we need more lines
            if (baseString.Length >= lineWidth - promptLength)
            {
                userText.Append('\n');
            }

            for (int x = firstLine.Count(); x <= userText.Length; x += lineWidth)
            {
                var line = new string(baseString.Skip(x).Take(lineWidth).ToArray());
                userText.Append(line);

                if (line.Count() == lineWidth)
                {
                    userText.Append("\n");
                }
            }

            // restore forced newlines
            userText = userText.Replace("\0", "\\\n");
        }

        public void Remove(int pos, int length, int promptLength, int lineWidth)
        {
            // check if we are on a newline boundary, if we are remove both the newline and the char
            if (userText[pos] == '\n')
            {
                userText = userText.Remove(pos -1, length+1);
            }
            else
            {
                userText = userText.Remove(pos, length);
            }

            ReflowText(promptLength, lineWidth);
        }

        public int Length => userText.Length;

        public int Lines => CalculateTotalNumberOfLines();

        public override string ToString() => userText.ToString();

        public bool IsForcedMultiline { get; set; } = false;
    }

    /// <summary>
    /// This class implements improved handling for Console access including:
    /// * multiline input
    /// * hotkeys
    /// </summary>
    public class ConsoleImproved
    {
        /// <summary>
        /// Gets the cursor position.
        /// </summary>
        /// <value>
        /// The cursor position.
        /// </value>
        public int CursorPosition { get; internal set; } = 0;

        /// <summary>
        /// Gets the key overrides.
        /// </summary>
        /// <value>
        /// The key overrides.
        /// </value>
        public List<KeyValuePair<ConsoleKeyEx, Func<ConsoleImproved, ConsoleKeyEx, Task<bool>>>> KeyOverrides { get; } = new List<KeyValuePair<ConsoleKeyEx, Func<ConsoleImproved, ConsoleKeyEx, Task<bool>>>>();

        /// <summary>
        /// Gets the shell.
        /// </summary>
        /// <value>
        /// The shell.
        /// </value>
        public API.Shell Shell { get; private set; }

        /// <summary>
        /// Gets the width of the screen
        /// </summary>
        /// <value>
        /// The width.
        /// </value>
        public int Width => implementation.WindowWidth;

        /// <summary>
        /// Gets or sets the user entered text.
        /// </summary>
        /// <value>
        /// The user entered text.
        /// </value>
        public string UserEnteredText
        {
            get => userText.ToString();
            set { userText = new ConsoleTextSpan(); userText.Append(value); }
        }

        /// <summary>
        /// Gets the user entered text position.
        /// </summary>
        /// <value>
        /// The user entered text position.
        /// </value>
        public int UserEnteredTextPosition
        {
            get => userText.GetLogicCursorPosition(currentLineIndex, CursorPosition, lastPromptLength);
        }

        /// <summary>
        /// An optional object to store state in
        /// </summary>
        public object Tag = null;

        private int lastPromptLength = 0;
        private ConsoleTextSpan userText = new Console.ConsoleTextSpan();
        private readonly IConsole implementation;
        private int currentHistoryIndex;
        private int lastRawPosition;
        private ConsoleKeyInfo lastKeyRead;
        private int currentLineIndex = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleImproved"/> class.
        /// </summary>
        /// <param name="consoleImplementation">The console implementation.</param>
        /// <param name="shell">The shell.</param>
        public ConsoleImproved(IConsole consoleImplementation, Dotnet.Shell.API.Shell shell)
        {
            implementation = consoleImplementation;
            Shell = shell;
            currentHistoryIndex = shell.History.Count != 0 ? shell.History.Count - 1 : 0;

            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Backspace), OnBackSpaceAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Delete), OnDeleteAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.LeftArrow), OnArrowAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.RightArrow), OnArrowAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.UpArrow), OnHistoryAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.DownArrow), OnHistoryAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Home), OnHomeAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.End), OnEndAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Enter), OnEnterAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.LeftArrow, ConsoleModifiers.Control), OnSkipBackwardsWordAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.RightArrow, ConsoleModifiers.Control), OnSkipForwardWordAsync);
            AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Backspace, ConsoleModifiers.Control), OnDeleteLastWordAsync);
        }

        /// <summary>
        /// Replaces the user text at the specified position.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="position">The position.</param>
        public void ReplaceUserEntryAtPosition(string text, int position)
        {
            using (new HideCursor(implementation))
            {
                if (position < userText.Length)
                {
                    userText.Remove(position, UserEnteredText.Length - position, lastPromptLength, Width - 1);
                }
                userText.Insert(position, text);

                ClearUserEntry();
                Write(userText.ToString(), true);
            }
        }

        /// <summary>
        /// Adds a key override.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="func">The function.</param>
        public void AddKeyOverride(ConsoleKeyEx key, Func<ConsoleImproved, ConsoleKeyEx, Task<bool>> func)
        {
            KeyOverrides.Add(new KeyValuePair<ConsoleKeyEx, Func<ConsoleImproved, ConsoleKeyEx, Task<bool>>>(key, func));
        }

        /// <summary>
        /// Writes the line.
        /// </summary>
        /// <param name="message">The message.</param>
        public void WriteLine(string message = "")
        {
            implementation.CursorLeft = CursorPosition;
            implementation.WriteLine(message);
            CursorPosition = 0;
        }

        /// <summary>
        /// Writes the line.
        /// </summary>
        /// <param name="text">The text.</param>
        public void WriteLine(ColorString text)
        {
            implementation.CursorLeft = CursorPosition;
            CursorPosition += text.Length;

            implementation.WriteLine(text.TextWithFormattingCharacters);
        }

        /// <summary>
        /// Writes the specified text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="multiline">if set to <c>true</c> [multiline].</param>
        public void Write(string text = "", bool multiline = false)
        {
            var textByNewline = text.Split('\n');
            bool isSingleLine = textByNewline.Length == 1;

            if (isSingleLine || !multiline) // only write upto the window edge if wrapText == false
            {
                text = text.Replace("\n", string.Empty);

                var maxTextToWrite = Math.Min(text.Length, implementation.WindowWidth - CursorPosition - 1);
                implementation.CursorLeft = CursorPosition;
                CursorPosition += maxTextToWrite;

                implementation.Write(text.Length != maxTextToWrite ? text.Substring(0, maxTextToWrite) : text);
            }
            else
            {
                // multiline
                var firstLine = textByNewline[0].Take(implementation.WindowWidth - lastPromptLength - 1).ToArray();
                implementation.WriteLine(new string(firstLine));

                for (int x = 1; x < textByNewline.Length - 1; x++)
                {
                    implementation.WriteLine(new string(textByNewline[x].Take(implementation.WindowWidth).ToArray()));
                }

                var lastLine = new string(textByNewline.Last().Take(implementation.WindowWidth).ToArray());
                implementation.Write(lastLine);
                currentLineIndex = textByNewline.Length - 1;

                CursorPosition = lastLine.Length;
            }
        }

        /// <summary>
        /// Writes the specified text.
        /// </summary>
        /// <param name="text">The text.</param>
        public void Write(ColorString text)
        {
            implementation.CursorLeft = CursorPosition;

            // write and remove any multiline padding characters
            implementation.Write(text.TextWithFormattingCharacters/*.Replace("\0", "")*/);

            CursorPosition += text.Length;
        }

        private async Task<ConsoleKeyEx> ReadKeyAsync(bool addToInternalBuffer = true, CancellationToken token = default)
        {
            lastRawPosition = (CursorPosition + 1 >= Width) ? 0 : CursorPosition + 1;

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

            lastKeyRead = key;

            // increment pos if this is printable
            if (addToInternalBuffer && !char.IsControl(key.KeyChar) && !key.Modifiers.HasFlag(ConsoleModifiers.Control) && !key.Modifiers.HasFlag(ConsoleModifiers.Alt))
            {
                var oldTextPosition = userText.GetLogicCursorPosition(currentLineIndex, CursorPosition, lastPromptLength);

                using (new HideCursor(implementation))
                {
                    // if adding a character would take us over the edge of the console window we need to add a newline
                    if ((lastRawPosition + 1) % implementation.WindowWidth == 0)
                    {
                        currentLineIndex++;
                        implementation.WriteLine(); // todo there may already be space?
                        //implementation.CursorLeft = 0;
                        CursorPosition = 0;

                        if (oldTextPosition == userText.Length)
                        {
                            // just append char
                            userText.Append(key.KeyChar);
                            userText.Append('\n');
                        }
                        else
                        {
                            userText.Insert(oldTextPosition, "\n" + key.KeyChar);
                        }
                    }
                    else
                    {

                        // we now need to determine if we have to do a full redraw or can just add a character
                        if (oldTextPosition == userText.Length)
                        {
                            // just append char
                            userText.Append(key.KeyChar);
                        }
                        else
                        {
                            // insert at specified position
                            userText.Insert(oldTextPosition, key.KeyChar, lastPromptLength, Width -1);

                            // now clear everything typed in by the user
                            var oldPos = CursorPosition;
                            var oldTop = implementation.CursorTop;
                            var oldCurrentLineInde = currentLineIndex;

                            // write from the start of the prompt
                            ClearUserEntry();
                            Write(userText.ToString(), userText.Lines != 0);

                            implementation.CursorTop = oldTop;
                            CursorPosition = oldPos;
                            currentLineIndex = oldCurrentLineInde;
                        }

                        CursorPosition++;
                        implementation.CursorLeft = CursorPosition;
                    }
                }
            }

            return new ConsoleKeyEx(key.Key, key.Modifiers);
        }

        internal async Task<string> GetCharAsync(CancellationToken cancellationToken = default)
        {
            var key = await ReadKeyAsync(false, cancellationToken);
            return key.Key.Value.ToString();
        }

        /// <summary>
        /// Gets the command tryed in by the user asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task</returns>
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
            // nop newlines
            var command = userText.ToString().Replace("\\\n", string.Empty).Replace("\n", string.Empty).Replace("\\\0", string.Empty).Replace("\0", string.Empty);

            foreach (var handler in Shell.CommandHandlers)
            {
                command = handler(command);
            }

            if (!string.IsNullOrWhiteSpace(command))
            {
                Shell.History.Add(new API.HistoryItem(command, DateTime.UtcNow));
                // This looks like an off but 1 but the history code is going to decremement this if you hit up
                currentHistoryIndex = Shell.History.Count;
            }

            return command;
        }

        /// <summary>
        /// Displays the prompt.
        /// </summary>
        /// <param name="forcePromptOntoNewLine">if set to <c>true</c> [force prompt onto new line].</param>
        public void DisplayPrompt(bool forcePromptOntoNewLine = true)
        {
            // first we need to check if std out has put us in an odd location
            // if it has, by say cat-ing a file without a newline we want to add a newline
            // the behaviour is different from bash but looks so much better and stops the next
            // logic clobbering stdout

            if (forcePromptOntoNewLine && implementation.CursorLeft != 0)
            {
                WriteLine();
            }

            Reset();

            implementation.SaveCursorPosition();

            var prompt = Shell.Prompt();
            Write(prompt);

            lastPromptLength = prompt.Text.Length;
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            userText = new ConsoleTextSpan();
            implementation.CursorLeft = 0;
            CursorPosition = 0;
            currentLineIndex = 0;
            lastPromptLength = 0;
        }

        /// <summary>
        /// Displays the prompt.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="forcePromptOntoNewLine">if set to <c>true</c> [force prompt onto new line].</param>
        public void DisplayPrompt(string text, bool forcePromptOntoNewLine)
        {
            DisplayPrompt(forcePromptOntoNewLine);

            userText.Append(text);
            Write(text, false);
        }

        /// <summary>
        /// Clears the user text entry from the screen.
        /// </summary>
        /// <param name="numLinesToClear">The number lines to clear.</param>
        public void ClearUserEntry(int numLinesToClear = -1)
        {
            MoveCursorToStartOfPrompt();
            implementation.ClearCurrentLine(lastPromptLength);

            bool isMultiLine = userText.Lines != 0;

            if (isMultiLine || numLinesToClear != -1)
            {
                var totalLines = Math.Max(userText.Lines, numLinesToClear);

                for (int x = 0; x < totalLines; x++)
                {
                    implementation.MoveCursorDown(1);
                    currentLineIndex++;
                    implementation.ClearCurrentLine();
                }

                MoveCursorToStartOfPrompt();
                currentLineIndex = 0;
            }
        }

        /// <summary>
        /// Ignores the last tab character
        /// </summary>
        public void IgnoreTab()
        {
            if (lastKeyRead.KeyChar == '\t')
            {
                implementation.CursorLeft = lastRawPosition -1;
            }
        }

        private Task<bool> OnBackSpaceAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                var userTextPosition = userText.GetLogicCursorPosition(currentLineIndex, CursorPosition, lastPromptLength);
                if (userTextPosition > 0)
                {
                    using (new HideCursor(implementation))
                    {
                        var originalCursorPos = CursorPosition;
                        var originalCursorTop = implementation.CursorTop;
                        var originalLineIndex = currentLineIndex;

                        userText.Remove(userTextPosition - 1, 1, lastPromptLength, Width -1);
                        ClearUserEntry(currentLineIndex);

                        Write(userText.ToString(), true);
                        currentLineIndex = originalLineIndex;

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

                            if (currentLineIndex < 0)
                            {
                                currentLineIndex = 0;
                            }
                        }

                        implementation.CursorLeft = CursorPosition;
                        implementation.CursorTop = originalCursorTop;
                    }
                }
                else
                {
                    implementation.RestoreCursorPosition();
                    DisplayPrompt(false);
                }

                return false;
            });
        }

        private Task<bool> OnDeleteAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                var userTextPosition = userText.GetLogicCursorPosition(currentLineIndex, CursorPosition, lastPromptLength);
                if (userTextPosition < userText.Length)
                {
                    using (new HideCursor(implementation))
                    {
                        var originalCursorPos = CursorPosition;
                        var originalCursorTop = implementation.CursorTop;
                        var originalLineIndex = currentLineIndex;

                        userText.Remove(userTextPosition, 1, lastPromptLength, Width -1);
                        ClearUserEntry();
                        CursorPosition = lastPromptLength;
                        Write(userText.ToString(), true);
                        currentLineIndex = originalLineIndex;

                        CursorPosition = originalCursorPos;
                        implementation.CursorLeft = CursorPosition;
                        implementation.CursorTop = originalCursorTop;
                    }
                }

                return false;
            });
        }

        private Task<bool> OnHomeAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() => {
                prompt.MoveCursorToStartOfPrompt();
                return false;
            });
        }

        private Task<bool> OnEndAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (userText.Lines > 0)
                {
                    var numberOfLinesToMoveDown = userText.Lines - currentLineIndex;

                    if (numberOfLinesToMoveDown != 0)
                    {
                        implementation.MoveCursorDown(numberOfLinesToMoveDown);
                    }

                    currentLineIndex = userText.Lines;

                    var lengthOfLastLine = userText.ToString().Split('\n').Last().Length;
                    CursorPosition = lengthOfLastLine;
                    implementation.CursorLeft = CursorPosition;
                }
                else
                {
                    CursorPosition = lastPromptLength + userText.Length;
                    implementation.CursorLeft = CursorPosition;
                }

                return false;
            });
        }

        private Task<bool> OnArrowAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() => {
                var userTextPosition = userText.GetLogicCursorPosition(currentLineIndex, CursorPosition, lastPromptLength);
                if (key.Key == ConsoleKey.LeftArrow)
                {
                    if (currentLineIndex == 0 && CursorPosition - 1 >= lastPromptLength || // single line
                        currentLineIndex > 0 && CursorPosition - 1 >= 0) // multiline case
                    {
                        CursorPosition--;
                    }
                    else if (currentLineIndex > 0 && !userText.IsForcedMultiline)
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
                    if (userTextPosition + 1 <= userText.Length && implementation.CursorLeft + 1 <= implementation.WindowWidth - 1)
                    {
                        // can advance in string and position would not go over the window edge
                        CursorPosition++;
                    }
                    else if (userTextPosition + 1 <= userText.Length && implementation.CursorLeft + 1 > implementation.WindowWidth - 1 && !userText.IsForcedMultiline)
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
                    currentHistoryIndex--;
                    if (currentHistoryIndex < 0)
                    {
                        currentHistoryIndex = 0;
                    }
                }
                else
                {
                    currentHistoryIndex++;
                    if (currentHistoryIndex >= Shell.History.Count)
                    {
                        currentHistoryIndex = Shell.History.Count - 1;
                    }
                }

                if (currentHistoryIndex < Shell.History.Count && currentHistoryIndex != -1)
                {
                    ClearUserEntry();
                    CursorPosition = lastPromptLength;
                    MoveCursorToStartOfPrompt();

                    var newCmd = Shell.History[currentHistoryIndex].CmdLine;

                    Write(newCmd, newCmd.Length + lastPromptLength > Width);
                    userText = new ConsoleTextSpan();
                    userText.Append(newCmd);
                }

                return false;
            });
        }

        private Task<bool> OnEnterAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                var userTextPosition = userText.GetLogicCursorPosition(currentLineIndex, CursorPosition, lastPromptLength);
                // as in Bash escaping can only be performed at the end of the line
                // ie you cant just jump to multiline in the middle of an existing command
                if (userText.Length != 0 && userTextPosition == userText.Length && userText[^1] == '\\')
                {
                    // force a newline
                    userText.Append('\n');
                    userText.IsForcedMultiline = true;

                    currentLineIndex++;
                    implementation.CursorLeft = 0;
                    CursorPosition = 0;
                    implementation.WriteLine(); // todo there may already be space?
                    return true;
                }
                return false;
            });
        }

        private Task<bool> OnSkipForwardWordAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(async () =>
            {
                var userTextPosition = userText.GetLogicCursorPosition(currentLineIndex, CursorPosition, lastPromptLength);
                if (userTextPosition + 1 < userText.Length)
                {
                    var spaceIndex = userText.ToString().IndexOf(' ', userTextPosition + 1);
                    if (spaceIndex == -1)
                    {
                        _ = await OnEndAsync(prompt, key);
                    }
                    else
                    {
                        var logicalCharsToAdvance = spaceIndex - userTextPosition;
                        for (int x = 0; x < logicalCharsToAdvance; x++)
                        {
                            _ = await OnArrowAsync(prompt, new ConsoleKeyEx(ConsoleKey.RightArrow));
                        }

                        // if we have jumped a line we might need an extra call
                        if (userText[userTextPosition] != ' ')
                        {
                            _ = await OnArrowAsync(prompt, new ConsoleKeyEx(ConsoleKey.RightArrow));
                        }
                    }
                }

                return false;
            });
        }

        private Task<bool> OnSkipBackwardsWordAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(async () =>
            {
                var userTextPosition = userText.GetLogicCursorPosition(currentLineIndex, CursorPosition, lastPromptLength);
                if (userTextPosition - 2 > 0)
                {
                    var spaceIndex = userText.ToString().LastIndexOf(' ', userTextPosition - 2);
                    if (spaceIndex == -1)
                    {
                        _ = await OnHomeAsync(prompt, key);
                    }
                    else
                    {
                        var logicalCharsToRetreat = userTextPosition - spaceIndex - 1;
                        for (int x = 0; x < logicalCharsToRetreat; x++)
                        {
                            _ = await OnArrowAsync(prompt, new ConsoleKeyEx(ConsoleKey.LeftArrow));
                        }
                    }
                }
                return false;
            });
        }

        private Task<bool> OnDeleteLastWordAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(async () =>
            {
                var userTextPosition = userText.GetLogicCursorPosition(currentLineIndex, CursorPosition, lastPromptLength);
                if (userTextPosition - 2 > 0)
                {
                    var spaceIndex = userText.ToString().LastIndexOf(' ', userTextPosition - 2);
                    if (spaceIndex == -1)
                    {
                        userText = new ConsoleTextSpan();
                        ClearUserEntry();
                        MoveCursorToStartOfPrompt();
                        CursorPosition = lastPromptLength;
                    }
                    else
                    {
                        var logicalCharsToRetreat = userTextPosition - spaceIndex - 1;
                        for (int x = 0; x < logicalCharsToRetreat; x++)
                        {
                            _ = await OnBackSpaceAsync(prompt, key);
                        }
                    }
                }
                return true;
            });
        }

        private void MoveCursorToStartOfPrompt()
        {
            // ugh when you go multiline the saved position it lost
            if (currentLineIndex != 0)
            {
                implementation.MoveCursorUp(currentLineIndex);
                implementation.CursorLeft = lastPromptLength;
                currentLineIndex = 0;
                CursorPosition = lastPromptLength;
                implementation.SaveCursorPosition();
            }
            else
            {
                implementation.RestoreCursorPosition(() =>
                {
                    implementation.CursorLeft = lastPromptLength;
                    currentLineIndex = 0;
                    CursorPosition = lastPromptLength;
                });
            }
        }
    }
}
