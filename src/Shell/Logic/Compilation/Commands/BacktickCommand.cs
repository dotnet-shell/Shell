using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UnitTests")]

namespace Dotnet.Shell.Logic.Compilation.Commands
{
    class BacktickCommand : IShellCommand
    {
        public const string MARKER = "#region SUBCOMMAND // ";
        public const string ENDMARKER = " #endregion";

        private readonly Regex UnescapedBackTicks = new(@"((?<![\\])[`])((?:.(?!(?<![\\])\1))*.?)\1", RegexOptions.Compiled);
        private readonly Regex UnescapedStrings = new(@"[^\\]*?[""](.+?[^\\]*?)[""]", RegexOptions.Compiled);
        private readonly Regex VariableAssignmentRegex = new(@"^[a-zA-Z<>]+\d*\s+[a-zA-Z]+\d*\s*=\s*`.+`\s*", RegexOptions.Compiled);

        public string GetCodeFromMetaRepresentation(string line)
        {
            return line.Replace(MARKER, string.Empty).Replace(ENDMARKER, string.Empty);
        }

        public string GetMetaRepresentation(string line)
        {
            var variableType = "string";
            if (VariableAssignmentRegex.IsMatch(line))
            {
                var splitBySpace = line.Split(new char[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (splitBySpace.First() != "var")
                {
                    variableType = splitBySpace.First();
                }
            }

            Queue<Tuple<string, bool>> components = new();
            var positions = new Queue<Tuple<int, int>>(GetBacktickedCommands(line).OrderBy(x => x.Item1));

            var lastEndPos = 0;
            while (positions.Any())
            {
                var currentPosition = positions.Dequeue();
                var backTickedStr = line.Substring(currentPosition.Item1, currentPosition.Item2);

                if (lastEndPos != currentPosition.Item1)
                {
                    var irrelevantStr = line.Substring(lastEndPos, currentPosition.Item1 - lastEndPos -1); // +1 for backtick
                    components.Enqueue(new Tuple<string, bool>(irrelevantStr, false));
                }
                components.Enqueue(new Tuple<string, bool>( backTickedStr, true ));

                lastEndPos = currentPosition.Item1 + currentPosition.Item2 +1; // +1 for the backtick
            }

            if (lastEndPos != line.Length)
            {
                components.Enqueue(new Tuple<string, bool>(line.Remove(0, lastEndPos), false));
            }

            var result = new StringBuilder();
            result.Append(MARKER);
            while (components.Any())
            {
                var currentComponent = components.Dequeue();
                if (currentComponent.Item2)
                {
                    result.Append(ShellCommand.BuildLine(currentComponent.Item1, false, null, variableType, Execution.Redirection.Out));
                }
                else
                {
                    result.Append(currentComponent.Item1);
                }
            }
            result.Append(ENDMARKER);

            return result.ToString();
        }

        public string GetMetaRepresentationMarker()
        {
            return MARKER;
        }

        public bool IsValid(string line)
        {
            return line.Contains("`");
        }

        internal List<Tuple<int, int>> GetBacktickedCommands(string input)
        {
            var ret = new List<Tuple<int, int>>();

            // first find the set of strings, backticked commands won't be inside these strings
            // for easier maintenance we replace these with spaces so we can see what is going on 
            foreach (Match match in UnescapedStrings.Matches(input))
            {
                var newString = new string(' ', match.Length);
                input = input.Remove(match.Index, match.Length).Insert(match.Index, newString);
            }

            foreach (Match match in UnescapedBackTicks.Matches(input))
            {
                foreach (Group group in match.Groups)
                {
                    if (!group.Value.Contains("`"))
                    {
                        ret.Add(new Tuple<int, int>(group.Index, group.Length));
                    }
                }
            }

            return ret;
        }
    }
}
