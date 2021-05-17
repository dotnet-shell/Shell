using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.Shell.Logic.Suggestions.Autocompletion
{
    class ExecutableCompletions
    {
        public static List<Suggestion> GetCompletions(string sanitizedText, API.Shell shell, int cursorPos)
        {
            var INVOKE_IN_DIR = "." + Path.DirectorySeparatorChar;

            var executableSanitizedInput = sanitizedText.Remove(0, INVOKE_IN_DIR.Length);

            var basePath = shell.WorkingDirectory;
            var startOfFilename = executableSanitizedInput;

            var lastDir = executableSanitizedInput.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastDir != -1)
            {
                basePath = Path.Combine(basePath, executableSanitizedInput.Substring(0, lastDir));
                startOfFilename = executableSanitizedInput.Remove(0, lastDir + 1);
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
    }
}
