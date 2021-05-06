using Microsoft.CodeAnalysis.CSharp;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class LoadCommand : IShellCommand
    {
        const string LOAD = "#region load //";
        const string ENDMARKER = " #endregion";

        public string GetCodeFromMetaRepresentation(string line)
        {
            var argument = line.Replace(LOAD, string.Empty).Replace(ENDMARKER, string.Empty).Trim();

            if (argument.Contains("$")) // argument is a variable, we need to execute through a wrapper to unpack the variable
            {
                return "await Shell.LoadScriptFromFileAsync(Shell.ConvertPathToAbsolute(" + ShellCommandUtilities.VariableExpansion(argument) + "));";
            }
            else
            {
                var escapedInput = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(argument)).ToFullString();
                return "await Shell.LoadScriptFromFileAsync(Shell.ConvertPathToAbsolute(" + escapedInput + "));";
            }
        }

        public string GetMetaRepresentation(string line)
        {
            return LOAD + line.Remove(0, 6);
        }

        public string GetMetaRepresentationMarker()
        {
            return LOAD;
        }

        public bool IsValid(string line)
        {
            return line.StartsWith("#load ");
        }
    }
}
