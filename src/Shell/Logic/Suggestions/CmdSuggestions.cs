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
        private readonly Task<string[]> commandsInPath;
        private readonly API.Shell shell;

        internal CmdSuggestions(API.Shell shell)
        {
            this.shell = shell;
            this.commandsInPath = Task.FromResult(new string[0]);
        }

        public CmdSuggestions(API.Shell shell, Task<string[]> commandsInPath)
        {
            this.shell = shell;
            this.commandsInPath = commandsInPath;
        }

        public async Task<IEnumerable<Suggestion>> GetSuggestionsAsync(string userText, int cursorPos)
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
                return await FileAndDirectoryCompletion.GetCompletionsAsync(sanitizedText, shell, cursorPos, commandsInPath);
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
