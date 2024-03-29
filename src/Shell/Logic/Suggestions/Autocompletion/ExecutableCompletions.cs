﻿using System;
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

            return GetFilesAndFolders(basePath)
                .Where(x => x.StartsWith(startOfFilename))
                // TODO we should check if the executable bit is set here
                .Distinct()
                .Select(x => new Suggestion() { Index = cursorPos, CompletionText = x.Remove(0, startOfFilename.Length), FullText = x })
                .ToList();
        }

        private static IEnumerable<string> GetFilesAndFolders(string basePath)
        {
            List<string> ret = new();

            try
            {
                ret.AddRange(Directory.GetFiles(basePath).Select(x => Path.GetFileName(x)));
            }
            catch
            {

            }

            try
            {
                ret.AddRange(Directory.GetDirectories(basePath).Select(x => Path.GetFileName(x) + Path.DirectorySeparatorChar));
            }
            catch
            {

            }

            return ret;
        }
    }
}
