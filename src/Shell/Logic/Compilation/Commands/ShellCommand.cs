using Dotnet.Shell.Logic.Execution;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class ShellCommand : IShellCommand
    {
        private const string CMD = "#region CMD //";
        public const string ENDMARKER = " #endregion";
        private static readonly Regex Vars = new(@"[$].+[$]", RegexOptions.Compiled);

        internal static string BuildLine(string commandToRun, bool async, string variableName = null, string returnType = null, Redirection redirection = Redirection.None)
        {
            var returnHandling = returnType == null ? "" : string.Format(".ConvertStdOutToVariable<{0}>()", returnType);
            var method = async ? "await Shell.ExecuteAsync" : "Shell.Execute";
            // argument is a variable, we need to execute through a wrapper to unpack the variable
            var cmd = Vars.IsMatch(commandToRun) ? 
                ShellCommandUtilities.VariableExpansion('"' + commandToRun + '"') : 
                SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(commandToRun)).ToFullString();
            var variable = !string.IsNullOrWhiteSpace(variableName) ? "var " + variableName + "=" : string.Empty;

            var redir = string.Empty;
            if (redirection.HasFlag(Redirection.Out) && redirection.HasFlag(Redirection.Err))
            {
                redir = ", Redirection.Out | Redirection.Err";
            } 
            else if (redirection.HasFlag(Redirection.Out))
            {
                redir = ", Redirection.Out";
            }
            else if (redirection.HasFlag(Redirection.Err))
            {
                redir = ", Redirection.Err";
            }

            return string.Format("{0}{1}({2}{3}){4}", variable, method, cmd, redir, returnHandling).Trim();
        }

        public string GetCodeFromMetaRepresentation(string line)
        {
            line = line.Replace(CMD, string.Empty).Replace(ENDMARKER, string.Empty);
            return "_= "+BuildLine(line, true)+";";
        }

        public string GetMetaRepresentation(string line)
        {
            return CMD+line+ENDMARKER;
        }

        public string GetMetaRepresentationMarker()
        {
            return CMD;
        }

        public bool IsValid(string line)
        {
            // matches what basically is a pretty weak regex for linux style commands
            // and does not start with a reserved word
            var regex = @"^\w+-*\w+([ ].+)*(?<!;)$";
            if (line.StartsWith("./") || Regex.IsMatch(line, regex))
            {
                return !ShellCommandUtilities.ReservedWords.Any(word => line.StartsWith(word));
            }
            return false;
        }
    }
}
