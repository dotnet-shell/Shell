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
        private Dotnet.Shell.API.Shell shell;

        public CmdSuggestions(Dotnet.Shell.API.Shell shell)
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
            sanitizedText = RemoveTextBeforeAndIncluding(sanitizedText, new string[] { "&&", ";" }).Trim();

            // c<TAB> -> cd [command]
            // ech<TAB> -> echo [command]
            // cat b<TAB> -> cat bob [file]
            // cat bob; ec[TAB] -> cat bob; echo [file]

            // todo instead of shell.WorkingDirectory we should suggest a path based on what the user typed in

            const string CD_MATCH = "cd ";
            string INVOKE_IN_DIR = "."+Path.DirectorySeparatorChar;

            if (sanitizedText.StartsWith(CD_MATCH))
            {
                var cdSanitizedInput = sanitizedText.Remove(0, CD_MATCH.Length);
                return Directory.GetDirectories(shell.WorkingDirectory)
                    .Select(x => Path.GetFileName(x))
                    .Where(x => x.StartsWith(cdSanitizedInput))
                    .Select(x => x.Remove(0, cdSanitizedInput.Length))
                    .Distinct()
                    .Select(x => new Suggestion() { Index = cursorPos, CompletionText = x+Path.DirectorySeparatorChar, FullText = x + Path.DirectorySeparatorChar })
                    .ToList();
            }
            else if (sanitizedText.StartsWith(INVOKE_IN_DIR))
            {
                var executableSanitizedInput = sanitizedText.Remove(0, INVOKE_IN_DIR.Length);

                var basePath = shell.WorkingDirectory;
                var startOfFilename = executableSanitizedInput;

                var lastDir = executableSanitizedInput.LastIndexOf(Path.DirectorySeparatorChar);
                if (lastDir != -1)
                {
                    basePath = Path.Combine(basePath, executableSanitizedInput.Substring(0, lastDir));
                    startOfFilename = executableSanitizedInput.Remove(0, lastDir+1);
                }

                return Directory.GetFiles(basePath)
                    .Select(x => Path.GetFileName(x))
                    .Where(x => x.StartsWith(startOfFilename))
                    .Select(x => x.Remove(0, startOfFilename.Length))
                    // TODO we should check if the executable bit is set here
                    .Distinct()
                    .Select(x => new Suggestion() { Index = cursorPos, CompletionText = x, FullText = x })
                    .ToList();
            }
            else
            {
                // if our cursor position is before a space then we are in command completion mode
                // otherwise we will complete with a filename

                // need to decide if we are going to look for a command, or a file
                var spacePos = sanitizedText.IndexOf(' ');
                bool suggestCommand = spacePos == -1 || spacePos >= cursorPos;

                if (suggestCommand)
                {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                    var matchedEndings = (await CommandsInPath).Where(x => x.StartsWith(sanitizedText)).Select(x => x.Remove(0, sanitizedText.Length)).Distinct().ToList();
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks

                    return matchedEndings.ConvertAll(x => new Suggestion() { CompletionText = x, Index = cursorPos, FullText = sanitizedText + x });
                }
                else
                {
                    // 'command arg1 arg2 /home/asdad/d<TAB>'

                    var fsStart = sanitizedText.LastIndexOf(' ', sanitizedText.Length -1); // todo change to regex and match multiple chars?
                    var startOfDirOrFile = sanitizedText.Remove(0, fsStart == -1 ? 0 : fsStart +1); // +1 for the space

                    var fullPath = ConvertToAbsolute(startOfDirOrFile);

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

            // user defined?
        }

        internal string ConvertToAbsolute(string partialDir)
        {
            return Path.GetFullPath(partialDir.Replace("~", shell.HomeDirectory), shell.WorkingDirectory);
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
