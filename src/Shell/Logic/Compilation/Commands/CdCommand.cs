using Microsoft.CodeAnalysis.CSharp;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class CdCommand : IShellCommand
    {
        public const string CD = "#region CD // ";
        public const string ENDMARKER = " #endregion";

        public string GetCodeFromMetaRepresentation(string line)
        {
            line = line.Replace(CD, string.Empty).Replace(ENDMARKER, string.Empty).Trim();
            var escapedInput = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(line)).ToFullString();
            return "Shell.ChangeDir(" + escapedInput + ");";
        }

        public string GetMetaRepresentation(string line)
        {
            return CD+(line.Remove(0, 2)).TrimEnd()+ENDMARKER;
        }

        public string GetMetaRepresentationMarker()
        {
            return CD;
        }

        public bool IsValid(string line)
        {
            return line.StartsWith("cd ") || line == "cd";
        }
    }
}
