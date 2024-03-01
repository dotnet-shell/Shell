using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;

[assembly: InternalsVisibleTo("UnitTests")]

namespace Dotnet.Shell.Logic.Suggestions
{
    internal class CSharpSuggestions
    {
        private readonly Task<string[]> commandsInPath;
        private readonly IEnumerable<MetadataReference> assemblies;
        private readonly Project project;
        private readonly Regex PowershellOrBashCommandRegex = new(@"^[\w-]+$", RegexOptions.Compiled);

        internal CSharpSuggestions() : this(Task.FromResult(Array.Empty<string>()))
        {

        }

        public CSharpSuggestions(Task<string[]> commandsInPath, IEnumerable<MetadataReference> assemblies = null)
        {
            this.commandsInPath = commandsInPath;
            if (assemblies == null)
            {
                assemblies = GetAllLoadedAssemblies();
            }
            this.assemblies = assemblies;

            var usings = new List<string>()
            {
                "System",
                "System.Collections",
                "System.Collections.Generic",
                "System.Linq",
                "System.Drawing",
                "System.IO",
                "Dotnet.Shell.UI"
            };
            usings.AddRange(Settings.Default.AdditionalUsings);

            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                        .WithOverflowChecks(true).WithOptimizationLevel(OptimizationLevel.Release)
                        .WithUsings(usings);

            var workspace = new AdhocWorkspace(); // dispose?
            string projName = "NewProject132";
            var projectId = ProjectId.CreateNewId();
            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projName,
                projName,
                LanguageNames.CSharp,
                isSubmission: true,
                compilationOptions: options,
                metadataReferences: this.assemblies,
                parseOptions: new CSharpParseOptions(kind: SourceCodeKind.Script, languageVersion: LanguageVersion.Latest));
            project = workspace.AddProject(projectInfo);
        }

        /// <summary>
        /// todo get this from executor
        /// </summary>
        /// <returns></returns>
        private static List<MetadataReference> GetAllLoadedAssemblies()
        {
            var refs = AppDomain.CurrentDomain.GetAssemblies();
            var references = new List<MetadataReference>();

            foreach (var reference in refs.Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location)))
            {
                var stream = new FileStream(reference.Location, FileMode.Open, FileAccess.Read);
                references.Add(MetadataReference.CreateFromStream(stream));
            }
            return references;
        }

        public async Task<IEnumerable<Suggestion>> GetSuggestionsAsync(string userText, int cursorPos)
        {
            if (cursorPos < 0 || cursorPos > userText.Length)
            {
                return null;
            }

            var sanitizedText = userText.Substring(0, cursorPos);

            // try and remove some cases where commands are incorrectly interpretted as C#
            // IP Addresses for commands like SSH are a good example here
            var possibleCommand = sanitizedText.Split(' ').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(possibleCommand) && PowershellOrBashCommandRegex.IsMatch(possibleCommand))
            {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                var cmds = await commandsInPath;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
                if (cmds.Contains(possibleCommand))
                {
                    return null;
                }
            }

            var id = DocumentId.CreateNewId(project.Id);

            var solution = project.Solution.AddDocument(id, project.Name, sanitizedText);
            var document = solution.GetDocument(id);

            return await GetCompletionResultsAsync(document, sanitizedText, sanitizedText.Length);
        }

        private static async Task<IEnumerable<Suggestion>> GetCompletionResultsAsync(Document document, string sanitizedText, int position)
        {
            var ret = new List<Suggestion>();

            var completionService = CompletionService.GetService(document);
            var results = await completionService.GetCompletionsAsync(document, position);

            if (results == null)
            {
                return new List<Suggestion>();
            }

            foreach (var i in results.ItemsList)
            {
                if (i.Properties.ContainsKey("SymbolKind") && i.Properties["SymbolKind"] == "9" &&
                    i.Properties.ContainsKey("InsertionText"))
                {
                    var text = i.Properties["InsertionText"];

                    var fullText = sanitizedText.Substring(0, i.Span.Start) + text;

                    if (fullText.StartsWith(sanitizedText))
                    {
                        ret.Add(new Suggestion() { Index = i.Span.Start, CompletionText = text, FullText = text });
                    }
                }
            }

            return ret;
        }
    }
}
