namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class ClsCommand : IShellCommand
    {
        public const string CLS = "#region cls";
        public const string ENDMARKER = " #endregion";

        public string GetCodeFromMetaRepresentation(string line)
        {
            return "Console.Clear();";
        }

        public string GetMetaRepresentation(string line)
        {
            return CLS+ENDMARKER;
        }

        public string GetMetaRepresentationMarker()
        {
            return CLS;
        }

        public bool IsValid(string line)
        {
            return line.StartsWith("#cls");
        }
    }
}
