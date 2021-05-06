using System;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class SheBangCommand : IShellCommand
    {
        private const string MARKER = "#!";

        public string GetCodeFromMetaRepresentation(string line)
        {
            throw new NotImplementedException();
        }

        public string GetMetaRepresentation(string line)
        {
            // basically deletes the line
            return string.Empty;
        }

        public string GetMetaRepresentationMarker()
        {
            return MARKER;
        }

        public bool IsValid(string line)
        {
            return line.StartsWith(MARKER);
        }
    }
}
