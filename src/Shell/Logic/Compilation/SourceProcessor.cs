using Dotnet.Shell.Logic.Compilation.Commands;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dotnet.Shell.Logic.Compilation
{

    internal class SourceProcessor
    {
        private CSharpParseOptions parseOptions = new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Script);
        private List<Func<string, Task<string>>> processingChain = new List<Func<string, Task<string>>>();
        private readonly List<IShellCommand> syntaxToProcess = new List<IShellCommand>();
        private readonly List<IShellBlockCommand> blockSyntaxToProcess = new List<IShellBlockCommand>();
        private readonly ShellCommand shellCommand;
        private readonly List<IShellCommand> postProcessingSyntax = new List<IShellCommand>();

        public SourceProcessor()
        {
            syntaxToProcess.AddRange(new IShellCommand[] {
                new SheBangCommand(),
                new ExitCommand(),
                new ClsCommand(),
                new RefCommand(),
                new LoadCommand(),
                new CdCommand(),
                new BacktickCommand(),
                new ResetCommand()
            });
            blockSyntaxToProcess.AddRange(new IShellBlockCommand[]
            {
                new CSharpRegion(),
                new CommandRegion()
            });
            shellCommand = new ShellCommand();
            postProcessingSyntax.AddRange(syntaxToProcess);
            postProcessingSyntax.Add(shellCommand);
            processingChain.Add(ReplaceBuiltInCommandsWithMetaAsync);
            processingChain.Add(StripAllCommentsAsync);
            processingChain.Add(FormatCodeAsync);
            processingChain.Add(BuildFinalSourceAsync);
        }

        private async Task<string> ReplaceBuiltInCommandsWithMetaAsync(string input)
        {
            var singleLineComments = GetComments(input).SingleLineComments;

            using (var sr = new StringReader(input))
            using (var sw = new StringWriter())
            {
                int lineNumber = 1;
                var line = await sr.ReadLineAsync();
                try
                {
                    var currentBlockCommands = new Dictionary<IShellBlockCommand, List<string>>();

                    while (line != null)
                    {
                        singleLineComments.Where(x => line.EndsWith(x)).ToList().ForEach(matchingComment => { line = line.Remove(line.Length - matchingComment.Length); });
                        var whitespaceRemovedLine = line.Trim();

                        // add the line to anything handling blocks
                        currentBlockCommands.Values.ToList().ForEach(x => x.Add(line));

                        // if there is a block which has found its end marker then process the command and remove it
                        var completedBlocksCommands = currentBlockCommands.Keys.Where(blockCommand => blockCommand.IsEnd(whitespaceRemovedLine)).ToList();
                        var newBlocks = blockSyntaxToProcess.Where(blockCommandParser => blockCommandParser.IsStart(whitespaceRemovedLine));
                        
                        if (currentBlockCommands.Any() || completedBlocksCommands.Any())
                        {
                            // if we are processing any block command or have emitted results from a block command then don't process anything
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates (If this fails something terrible has happened anyway)
                            completedBlocksCommands.ForEach(async x => await sw.WriteLineAsync(x.GetMetaRepresentation(currentBlockCommands[x])));
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
                            completedBlocksCommands.ForEach(x => currentBlockCommands.Remove(x));
                        }
                        else if (newBlocks.Any())
                        {
                            if (newBlocks.Count() > 1)
                            {
                                throw new PreProcessorSyntaxException("Error, multiple blocks match a single line", lineNumber);
                            }
                            else
                            {
                                currentBlockCommands.Add(newBlocks.First(), new List<string>() { line });
                            }
                        }
                        else if (string.IsNullOrWhiteSpace(line) || LooksLikeCSharp(whitespaceRemovedLine))
                        {
                            await sw.WriteLineAsync(line);
                        }
                        else
                        {
                            var builtinCommand = syntaxToProcess.FirstOrDefault(x => x.IsValid(whitespaceRemovedLine));

                            if (builtinCommand != null)
                            {
                                await sw.WriteLineAsync(builtinCommand.GetMetaRepresentation(whitespaceRemovedLine));
                            }
                            else if (shellCommand != null && shellCommand.IsValid(whitespaceRemovedLine))
                            {
                                await sw.WriteLineAsync(shellCommand.GetMetaRepresentation(whitespaceRemovedLine));
                            }
                            else
                            {
                                await sw.WriteLineAsync(line);
                            }
                        }

                        line = await sr.ReadLineAsync();
                        lineNumber++;
                    }
                }
                catch (Exception ex)
                {
                    throw new PreProcessorSyntaxException(ex.Message, lineNumber);
                }

                return sw.ToString();
            }
        }

        private static bool LooksLikeCSharp(string line)
        {
            // todo remove any strings in the line

            var csharpEndChars = new char[] { ';', '{', '}', '(', ')' }; 

            // This regex matches assignment to a variable from a command in the form
            // var x = `ls`;
            // There are some obvious issues here:
            // * c# is more flexible with variable names
            // * only one variable assignment can be on a line
            var CmdToVariableNoSemiColonRegex = new Regex(@"^[a-zA-Z]+\d*\s+[a-zA-Z]+\d*\s*=\s*`.+`\s*$");

            if (CmdToVariableNoSemiColonRegex.IsMatch(line))
            {
                throw new Exception("Missing ; : "+line);
            }
            else if (line.Count(x => x == '`') > 1)
            {
                return false;
            }
            else if (csharpEndChars.Any(x => line.EndsWith(x)) || ShellCommandUtilities.ReservedWords.Any(word => line.StartsWith(word)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private CommentWalker GetComments(string text)
        {
            var tree = CSharpSyntaxTree.ParseText(text, parseOptions);
            var root = tree.GetCompilationUnitRoot();

            var walker = new CommentWalker();
            walker.Visit(root);

            return walker;
        }

        private Task<string> StripAllCommentsAsync(string text)
        {
            return Task.Run(() =>
            {
                var walker = GetComments(text);

                var changes = walker.Comments.ConvertAll(comment => new TextChange(comment.SourceSpan, string.Empty));

                if (changes.Any())
                {
                    var source = SourceText.From(text);
                    return source.WithChanges(changes).ToString();
                }
                else
                {
                    return text;
                }            
            });
        }

        private Task<string> FormatCodeAsync(string text)
        {
            return Task.Run(() =>
            {
                var source = SourceText.From(text);
                var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
                var root = tree.GetCompilationUnitRoot();

                // now we reformat the source tree to make it consistent for the preprocessor to handle it
                var workspace = new AdhocWorkspace();
                OptionSet options = workspace.Options;

                SyntaxNode formattedNode = Formatter.Format(root, workspace, options);

                // emit some new source text but also replace `" with `
                using (var tw = new StringWriter())
                {
                    formattedNode.WriteTo(tw);
                    return tw.ToString();
                }
            });
        }

        private async Task<string> BuildFinalSourceAsync(string input)
        {
            using (var sr = new StringReader(input))
            using (var sw = new StringWriter())
            {
                var line = await sr.ReadLineAsync();
                while (line != null)
                {
                    line = line.TrimStart();
                    var cmds = postProcessingSyntax.Where(x => line.StartsWith(x.GetMetaRepresentationMarker()));

                    if (cmds.Any())
                    {
                        if (cmds.Count() != 1)
                        {
                            throw new PreProcessorSyntaxException("Duplicate format: "+string.Join(", ", cmds.Select(x => x.GetMetaRepresentationMarker())), 0);
                        }
                        await sw.WriteLineAsync(cmds.First().GetCodeFromMetaRepresentation(line));
                    }
                    else
                    {
                        await sw.WriteLineAsync(line);
                    }

                    line = await sr.ReadLineAsync();
                }

                return sw.ToString();
            }
        }

        public async Task<string> ProcessAsync(string script)
        {
            foreach (var step in processingChain)
            {
                script = await step(script);
            }
            return script.Trim();
        }
    }
}
