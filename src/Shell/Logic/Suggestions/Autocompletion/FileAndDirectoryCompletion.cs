using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("UnitTests")]

namespace Dotnet.Shell.Logic.Suggestions.Autocompletion
{
    class FileAndDirectoryCompletion
    {
        public static async Task<List<Suggestion>> GetCompletionsAsync(string sanitizedText, API.Shell shell, int cursorPos, Task<string[]> commandsInPath)
        {
            // if our cursor position is before a space then we are in command completion mode
            // otherwise we will complete with a filename

            // need to decide if we are going to look for a command, or a file
            var spacePos = sanitizedText.IndexOf(' ');
            bool suggestCommand = spacePos == -1 || spacePos >= cursorPos;

            if (suggestCommand)
            {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                var matchedEndings = (await commandsInPath).Where(x => x.StartsWith(sanitizedText)).Select(x => x.Remove(0, sanitizedText.Length)).Distinct().ToList();
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks

                return matchedEndings.ConvertAll(x => new Suggestion() { CompletionText = x, Index = cursorPos, FullText = sanitizedText + x });
            }
            else
            {
                // 'command arg1 arg2 /home/asdad/d<TAB>'

                var fsStart = sanitizedText.LastIndexOf(' ', sanitizedText.Length - 1); // todo change to regex and match multiple chars?
                var startOfDirOrFile = sanitizedText.Remove(0, fsStart == -1 ? 0 : fsStart + 1); // +1 for the space

                var fullPath = ConvertToAbsolute(startOfDirOrFile, shell);

                var directoryName = Path.GetDirectoryName(fullPath);
                if (directoryName == null)
                {
                    directoryName = Dotnet.Shell.API.Shell.BasePath;
                }

                var toMatch = Path.GetFileName(fullPath);

                // /ho<TAB>
                // ./home<TAB>
                // ../<TAB>
                // bob/asdads<TAB>

                // suggest a file or directory
                List<string> items = new List<string>();
                try
                {
                    items.AddRange(Directory.GetFiles(directoryName).Select(x => Path.GetFileName(x)));
                }
                catch
                {

                }

                try
                {
                    items.AddRange(Directory.GetDirectories(directoryName).Select(x => Path.GetFileName(x) + Path.DirectorySeparatorChar));
                }
                catch
                {

                }

                return items
                    .Where(x => string.IsNullOrWhiteSpace(toMatch) || x.StartsWith(toMatch))
                    .Select(x => x.Remove(0, toMatch.Length))
                    .Distinct()
                    .Select(x => new Suggestion() { Index = cursorPos, CompletionText = x, FullText = toMatch + x }).ToList();
            }
        }

        internal static string ConvertToAbsolute(string partialDir, API.Shell shell)
        {
            return Path.GetFullPath(partialDir.Replace("~", shell.HomeDirectory), shell.WorkingDirectory);
        }
    }
}
