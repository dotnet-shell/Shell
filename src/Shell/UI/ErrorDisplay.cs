using System;
using System.Diagnostics;
using Dotnet.Shell.Logic.Console;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Dotnet.Shell.UI
{
    public class ErrorDisplay : IOptionsMonitor<ConsoleLoggerOptions>
    {
        private readonly IConsole console;
        private readonly ConsoleLoggerOptions _consoleLoggerOptions;

        public ErrorDisplay(IConsole console)
        {
            this.console = console;

            _consoleLoggerOptions = new ConsoleLoggerOptions()
            {
                LogToStandardErrorThreshold = LogLevel.Trace
            };
        }

        public void PrettyException(Exception ex, string input = default, string originalInput = default)
        {
            try
            {
                if (input != null && originalInput != null && ParseExceptionForErrorPosition(ex, out int line, out int charPos))
                {
                    var badLine = input.Split(Environment.NewLine)[line];
                    var beforeBadPos = badLine.Substring(0, charPos);
                    var afterBadPos = badLine.Remove(0, charPos);
                    var displayStr = beforeBadPos + new ColorString(afterBadPos, System.Drawing.Color.Red);

                    Console.WriteLine(ex.Message);
                    PrettyError(displayStr);

                    try
                    {
                        var originalLine = originalInput.Split(Environment.NewLine)[line];
                        console.WriteLine("Original input line: " + originalLine);
                    }
                    catch (Exception)
                    {
                        console.WriteLine("Original input line: " + input);
                    }
                }
                else
                {
                    PrettyError(ex.Message);
                }
            }
            catch (Exception)
            {
                console.WriteLine(new ColorString(ex.Message, System.Drawing.Color.Red).TextWithFormattingCharacters);

                if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                {
                    foreach (var line in ex.StackTrace.Split(Environment.NewLine))
                    {
                        var trimmedLine = line.TrimStart();
                        if (!trimmedLine.StartsWith("at Microsoft.CodeAnalysis.Scripting.") &&
                            !trimmedLine.StartsWith("at Dotnet.Shell.Logic.Compilation."))
                        {
                            console.WriteLine(new ColorString(line, System.Drawing.Color.Yellow).TextWithFormattingCharacters);
                        }
                    }
                }

                Debugger.Break();
            }
        }

        public void PrettyError(ColorString msg)
        {
            console.WriteLine(msg.TextWithFormattingCharacters);
        }

        public void PrettyError(string msg)
        {
            console.WriteLine(new ColorString(msg, System.Drawing.Color.Red).TextWithFormattingCharacters);
        }

        private static bool ParseExceptionForErrorPosition(Exception ex, out int line, out int charPos)
        {
            line = -1;
            charPos = -1;
            try
            {
                if (ex.Message.StartsWith("(") && ex.Message.Contains(")"))
                {
                    var lineAndChar = ex.Message[1..ex.Message.IndexOf(")")];
                    var lineAndCharSplit = lineAndChar.Split(",");
                    line = int.Parse(lineAndCharSplit[0]) - 1;
                    charPos = int.Parse(lineAndCharSplit[1]);

                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public ConsoleLoggerOptions CurrentValue => _consoleLoggerOptions;

        public ConsoleLoggerOptions Get(string name) => _consoleLoggerOptions;

        public IDisposable OnChange(Action<ConsoleLoggerOptions, string> listener)
        {
            return null;
        }

        public static void PrettyInfo(string line)
        {
            Console.WriteLine(line);
        }
    }
}
