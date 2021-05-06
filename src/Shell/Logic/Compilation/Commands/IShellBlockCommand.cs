using System.Collections.Generic;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    interface IShellBlockCommand
    {
        bool IsStart(string line);

        bool IsEnd(string line);

        string GetMetaRepresentation(IList<string> lines);

        string GetCodeFromMetaRepresentation(string line);

        public string GetMetaRepresentationMarker();
    }
}
