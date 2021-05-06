using System;
using System.Collections.Generic;
using System.Linq;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class CSharpRegion : IShellBlockCommand
    {
        private const string MARKER = "#region c#";
        private const string ENDMARKER = "#endregion";

        public string GetCodeFromMetaRepresentation(string line)
        {
            throw new NotImplementedException();
        }

        public string GetMetaRepresentation(IList<string> lines)
        {
            // all we need to do is throw away the first and last lines
            // GetCode() will never be called here as we never return a meta representation
            return string.Join(Environment.NewLine, lines.Skip(1).Take(lines.Count - 2));
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
            // this check will never actually work as our meta representation is the final
            // representation
            return MARKER;
        }
    }
}
