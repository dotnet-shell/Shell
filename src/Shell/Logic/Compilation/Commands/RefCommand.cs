using System;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class RefCommand : IShellCommand
    {
        const string REF = "#region r //";

        public string GetCodeFromMetaRepresentation(string line)
        {
            throw new NotImplementedException();
        }

        public string GetMetaRepresentation(string line)
        {
            // return ourselves, GetCodeFromMetaRepresentation won't be called
            return line;
        }

        public string GetMetaRepresentationMarker()
        {
            // #r is basically supported now
            return REF;
        }

        public bool IsValid(string line)
        {
            return line.StartsWith("#r ");
        }
    }
}
