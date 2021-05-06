namespace Dotnet.Shell.Logic.Compilation.Commands
{
    interface IShellCommand
    {
        bool IsValid(string line);

        string GetMetaRepresentation(string line);

        string GetCodeFromMetaRepresentation(string line);

        string GetMetaRepresentationMarker();
    }
}
