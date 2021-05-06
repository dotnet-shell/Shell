using System;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class ExitCommand : IShellCommand
    {
        public const string Exit = "#region exit #endregion";

        public string GetCodeFromMetaRepresentation(string line)
        {
            return "throw new ExitException();";
        }

        public string GetMetaRepresentation(string line)
        {
            return Exit;
        }

        public string GetMetaRepresentationMarker()
        {
            return Exit;
        }

        public bool IsValid(string line)
        {
            return (line.StartsWith("#exit") || line == "exit");
        }
    }
}
