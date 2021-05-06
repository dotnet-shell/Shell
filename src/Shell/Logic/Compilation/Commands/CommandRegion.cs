using System;
using System.Collections.Generic;
using System.Linq;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class CommandRegion : IShellBlockCommand
    {
        private const string MARKER = "#region cmd";
        private const string ENDMARKER = "#endregion";

        public string GetCodeFromMetaRepresentation(string line)
        {
            throw new NotImplementedException();
        }

        public string GetMetaRepresentation(IList<string> lines)
        {
            var linesBetweenMarkers = string.Join(Environment.NewLine, lines.Skip(1).Take(lines.Count - 2));

            var script = string.Join(" && ", linesBetweenMarkers).Trim();

            return new ShellCommand().GetMetaRepresentation(script);
        }

        public bool IsEnd(string line)
        {
            return line.StartsWith(ENDMARKER);
        }

        public bool IsStart(string line)
        {
            return line.StartsWith(MARKER);
        }

        public string GetMetaRepresentationMarker()
        {
            // GetCodeFromMetaRepresentation won't be returned as we diver to ShellCommands meta
            return MARKER;
        }
    }
}
