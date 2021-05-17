using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Dotnet.Shell.Logic.Suggestions.Autocompletion
{
    class CdCompletion
    {
        public static List<Suggestion> GetCompletions(string sanitizedText, API.Shell shell, int cursorPos)
        {
            const string CD_MATCH = "cd";
            var cdSanitizedInput = sanitizedText.Remove(0, CD_MATCH.Length).TrimStart();

            bool isAbsolutePath = cdSanitizedInput.StartsWith(Path.DirectorySeparatorChar) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && cdSanitizedInput.Length >= 3 && cdSanitizedInput[1] == ':';

            if (string.IsNullOrWhiteSpace(cdSanitizedInput) || (isAbsolutePath && Directory.Exists(cdSanitizedInput)))
            {
                return
                    TryGetDirectories(isAbsolutePath ? cdSanitizedInput : shell.WorkingDirectory)
                    .Select(x => new Suggestion() { Index = cursorPos, CompletionText = Path.GetFileName(x) + Path.DirectorySeparatorChar, FullText = Path.GetFileName(x) + Path.DirectorySeparatorChar })
                    .ToList();
            }
            else if (isAbsolutePath) // absolute paths
            {
                return
                    TryGetDirectories(Path.GetDirectoryName(cdSanitizedInput))
                    .Where(x => x.StartsWith(cdSanitizedInput))
                    .Select(x => x.Remove(0, cdSanitizedInput.Length))
                    .Distinct()
                    .Select(x => new Suggestion() { Index = cursorPos, CompletionText = x + Path.DirectorySeparatorChar, FullText = cdSanitizedInput + x + Path.DirectorySeparatorChar })
                    .ToList();
            }
            else if (cdSanitizedInput.EndsWith(Path.DirectorySeparatorChar) || cdSanitizedInput.StartsWith(".." + Path.DirectorySeparatorChar) || cdSanitizedInput.StartsWith("." + Path.DirectorySeparatorChar)) // sub directories
            {
                return
                    TryGetDirectories(Path.Combine(shell.WorkingDirectory, cdSanitizedInput))
                    .Select(x => Path.GetFileName(x))
                    .Distinct()
                    .Select(x => new Suggestion() { Index = cursorPos, CompletionText = x + Path.DirectorySeparatorChar, FullText = cdSanitizedInput + x + Path.DirectorySeparatorChar })
                    .ToList();
            }
            else if (cdSanitizedInput.Contains(Path.DirectorySeparatorChar)) // sub directories based on CWD
            {
                var dirName = Path.GetFileName(cdSanitizedInput);

                return
                    TryGetDirectories(Path.Combine(shell.WorkingDirectory, Path.GetDirectoryName(cdSanitizedInput)))
                    .Select(x => Path.GetFileName(x))
                    .Where(x => x.StartsWith(dirName))
                    .Select(x => x.Remove(0, dirName.Length))
                    .Distinct()
                    .Select(x => new Suggestion() { Index = cursorPos, CompletionText = x + Path.DirectorySeparatorChar, FullText = cdSanitizedInput + x + Path.DirectorySeparatorChar })
                    .ToList();
            }
            else // based from working dir
            {
                return TryGetDirectories(shell.WorkingDirectory)
                    .Select(x => Path.GetFileName(x))
                    .Where(x => x.StartsWith(cdSanitizedInput))
                    .Select(x => x.Remove(0, cdSanitizedInput.Length))
                    .Distinct()
                    .Select(x => new Suggestion() { Index = cursorPos, CompletionText = x + Path.DirectorySeparatorChar, FullText = cdSanitizedInput + x + Path.DirectorySeparatorChar })
                    .ToList();
            }
        }

        private static IEnumerable<string> TryGetDirectories(string location)
        {
            try
            {
                return Directory.GetDirectories(location);
            }
            catch
            {
                return new string[] { };
            }
        }
    }
}
