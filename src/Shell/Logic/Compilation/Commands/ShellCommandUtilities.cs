using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    internal class ShellCommandUtilities
    {
        internal static readonly string[] ReservedWords = new[] {
            "public ",
            "private ",
            "protected ",
            "internal ",
            "class ",
            "static ",
            "if ",
            "else",
            "while ",
            "for ",
            "foreach ",
            "abstract ",
            "base ",
            "delegate ",
            "lock ",
            "interface ",
            "namespace ",
            "switch ",
            "case ",
            "default:",
            "void ",
            "return ",
        };

        internal static string VariableExpansion(string shellCommand)
        {
            // What is this nightmare regex?
            // * Match anything between two $
            // * But NOT if the character before the $ is a \
            // * But NOT if there is a $ inside the two $

            /*
             * Matches
            hello my name is $bob$
            hello my name is $bob1$
            hello my name is $b$ob$ only ob

            Does not match
            hello my name is "$hello"
            hello my name is \$bob\$
            */

            //var rx = new Regex(@"[^\\]*([$][^$]+[^\\][$])");
            var rx = new Regex(@".*([^\\]*[$].+[^\\]*[$]).*");
            Match match = rx.Match(shellCommand);

            if (match == null || !match.Success || match.Groups.Count == 0)
            {
                return shellCommand;
            }

            // start with the smallest replacement and work up
            var matchGroup = match.Groups.Values.OrderBy(x => x.Length).First();

            var possibleVariableName = matchGroup.Value.Trim('$');
            var startPos = matchGroup.Index;

            // remove $varName$
            shellCommand = shellCommand.Remove(startPos, matchGroup.Value.Length);

            var strToInsert = "\"+" + possibleVariableName + "+\"";

            shellCommand = shellCommand.Insert(startPos, strToInsert);

            // check for more variables
            shellCommand = VariableExpansion(shellCommand);

            const string EmptyStrEnd = "+\"";
            const string EmptyStrStart = "\"+";
            const string DoubleEmptyStrEnd = "+\"\"";
            const string DoubleEmptyStrStart = "\"\"+";
            if (shellCommand.EndsWith(EmptyStrEnd))
            {
                shellCommand = shellCommand.Remove(shellCommand.Length - EmptyStrEnd.Length);
            }
            if (shellCommand.StartsWith(EmptyStrStart))
            {
                shellCommand = shellCommand.Remove(0, EmptyStrEnd.Length);
            }

            if (shellCommand.EndsWith(DoubleEmptyStrEnd))
            {
                shellCommand = shellCommand.Remove(shellCommand.Length - DoubleEmptyStrEnd.Length);
            }
            if (shellCommand.StartsWith(DoubleEmptyStrStart))
            {
                shellCommand = shellCommand.Remove(0, DoubleEmptyStrStart.Length);
            }

            return shellCommand;
        }
    }
}
