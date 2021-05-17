using Dotnet.Shell.Logic.Suggestions.Autocompletion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("UnitTests")]

namespace Dotnet.Shell.Logic.Suggestions
{
    internal class CmdSuggestions
    {
        private Task<string[]> CommandsInPath;
        private API.Shell shell;

        public CmdSuggestions(API.Shell shell)
        {
            this.shell = shell;

            CommandsInPath = Task.Run(() =>
            {
                var ret = new List<string>();

                ret.AddRange(shell.cmdAliases.Keys);
                ret.AddRange(shell.csAliases.Keys);

                foreach (var path in shell.Paths)
                {
                    ret.AddRange(Directory.GetFiles(path).Select(x=> x.Remove(0, path.Length+1)));
                    // todo check executable bit
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    const string EXE = ".exe";
                    return ret.Where(x => x.EndsWith(EXE)).Select(x => x.Substring(0, x.Length - EXE.Length)).OrderBy(x => x.Length).ToArray();
                }
                else
                {
                    return ret.OrderBy(x => x.Length).ToArray();
                }
            });
        }

        public async Task<List<Suggestion>> GetSuggestionsAsync(string userText, int cursorPos)
        {
            if (cursorPos < 0 || cursorPos > userText.Length)
            {
                return new List<Suggestion>();
            }

            var sanitizedText = userText.Substring(0, cursorPos);

            // first, remove anything that might be part of another command
            // look backward for the follow characters and forget everything before them
            // && ; 
            sanitizedText = RemoveTextBeforeAndIncluding(sanitizedText, new string[] { "&&", ";" }).Replace("~", shell.HomeDirectory).Trim();

            // c<TAB> -> cd [command]
            // ech<TAB> -> echo [command]
            // cat b<TAB> -> cat bob [file]
            // cat bob; ec[TAB] -> cat bob; echo [file]

            if (sanitizedText.StartsWith("cd"))
            {
                return CdCompletion.GetCompletions(sanitizedText, shell, cursorPos);
            }
            else if (sanitizedText.StartsWith("." + Path.DirectorySeparatorChar))
            {
                return ExecutableCompletions.GetCompletions(sanitizedText, shell, cursorPos);
            }
            else
            {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                return await FileAndDirectoryCompletion.GetCompletionsAsync(sanitizedText, shell, cursorPos, CommandsInPath);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
            }

            // user defined?
        }

        private static string RemoveTextBeforeAndIncluding(string userText, string[] markers)
        {
            var ret = userText;

            foreach (var marker in markers)
            {
                var index = ret.LastIndexOf(marker);
                if (index != -1)
                {
                    ret = ret.Remove(0, index +1);
                }
            }

            return ret;
        }
    }
}
