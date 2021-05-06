using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Dotnet.Script.Core;
using Dotnet.Script.DependencyModel.Context;
using Dotnet.Script.DependencyModel.Logging;
using Dotnet.Script.DependencyModel.NuGet;
using Dotnet.Shell.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Dotnet.Shell.Logic.Compilation
{
    /// <summary>
    /// This class integrates CSXShell with Dotnet Script.
    /// It is based on a simplified version of https://github.com/filipw/dotnet-script/blob/master/src/Dotnet.Script.Core/Interactive/InteractiveRunner.cs
    /// </summary>
    internal class InteractiveRunner
    {
        private ScriptState<object> scriptState;
        private ScriptOptions scriptOptions;

        private readonly InteractiveScriptGlobals globals;
        private readonly string[] packageSources = Array.Empty<string>();

        protected Logger logger;
        protected ScriptCompiler scriptCompiler;
        protected ScriptConsole console = ScriptConsole.Default;
        protected CSharpParseOptions parseOptions = new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Script);
        protected InteractiveCommandProvider interactiveCommandParser = new InteractiveCommandProvider();

        public ImmutableDictionary<string, object> ScriptVariables 
        {
            get
            {
                var vars = new Dictionary<string, object>();
                foreach (var variable in this.scriptState.Variables)
                {
                    if (!vars.ContainsKey(variable.Name))
                    {
                        vars.Add(variable.Name, variable.Value);
                    }
                }
                return vars.ToImmutableDictionary();
            }
        }

        public InteractiveRunner(ErrorDisplay errorDisplay)
        {
            var logFactory = CreateLogFactory("WARNING", errorDisplay);
            logger = logFactory.CreateLogger<InteractiveRunner>();

            globals = new InteractiveScriptGlobals(console.Out, CSharpObjectFormatter.Instance);
            scriptCompiler = new ScriptCompiler(logFactory, false);
        }

        public async Task<object> ExecuteAsync(string input, string workingDirectory)
        {
            if (scriptState == null)
            {
                var sourceText = SourceText.From(input);
                var context = new ScriptContext(sourceText, workingDirectory, Enumerable.Empty<string>(), scriptMode: ScriptMode.REPL, packageSources: packageSources);
                await RunFirstScriptAsync(context);
            }
            else
            {
                if (input.StartsWith("#r ") || input.StartsWith("#load "))
                {
                    var lineRuntimeDependencies = scriptCompiler.RuntimeDependencyResolver.GetDependenciesForCode(workingDirectory, ScriptMode.REPL, packageSources, input).ToArray();
                    var lineDependencies = lineRuntimeDependencies.SelectMany(rtd => rtd.Assemblies).Distinct();

                    var scriptMap = lineRuntimeDependencies.ToDictionary(rdt => rdt.Name, rdt => rdt.Scripts);
                    if (scriptMap.Count > 0)
                    {
                        scriptOptions =
                            scriptOptions.WithSourceResolver(
                                new NuGetSourceReferenceResolver(
                                    new SourceFileResolver(ImmutableArray<string>.Empty, workingDirectory), scriptMap));
                    }
                    foreach (var runtimeDependency in lineDependencies)
                    {
                        logger.Debug("Adding reference to a runtime dependency => " + runtimeDependency);
                        scriptOptions = scriptOptions.AddReferences(MetadataReference.CreateFromFile(runtimeDependency.Path));
                    }
                }
                scriptState = await scriptState.ContinueWithAsync(input, scriptOptions);
            }

            return scriptState.ReturnValue;
        }

        private async Task RunFirstScriptAsync(ScriptContext scriptContext)
        {
            foreach (var arg in scriptContext.Args)
            {
                globals.Args.Add(arg);
            }

            var compilationContext = scriptCompiler.CreateCompilationContext<object, InteractiveScriptGlobals>(scriptContext);
            console.WriteDiagnostics(compilationContext.Warnings, compilationContext.Errors);

            if (compilationContext.Errors.Any())
            {
                throw new CompilationErrorException("Script compilation failed due to one or more errors.", compilationContext.Errors.ToImmutableArray());
            }

            scriptState = await compilationContext.Script.RunAsync(globals, ex => true).ConfigureAwait(false);
            scriptOptions = compilationContext.ScriptOptions;
        }

        private static LogFactory CreateLogFactory(string verbosity, ErrorDisplay errorDisplay)
        {
            var logLevel = (Microsoft.Extensions.Logging.LogLevel)LevelMapper.FromString(verbosity);

            var loggerFilterOptions = new LoggerFilterOptions() { MinLevel = logLevel };

            var consoleLoggerProvider = new ConsoleLoggerProvider(errorDisplay);

            var loggerFactory = new LoggerFactory(new[] { consoleLoggerProvider }, loggerFilterOptions);

            return type =>
            {
                var logger = loggerFactory.CreateLogger(type);
                return (level, message, exception) =>
                {
                    logger.Log((Microsoft.Extensions.Logging.LogLevel)level, message, exception);
                };
            };
        }
    }
}