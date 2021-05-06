namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class ResetCommand : IShellCommand
    {
        public const string RESET = "#region reset";
        public const string ENDMARKER = " #endregion";

        public string GetCodeFromMetaRepresentation(string line)
        {
            return "#reset";
        }

        public string GetMetaRepresentation(string line)
        {
            return RESET + ENDMARKER;
        }

        public string GetMetaRepresentationMarker()
        {
            return RESET;
        }

        public bool IsValid(string line)
        {
            return line.StartsWith("#reset");
        }
    }
}
