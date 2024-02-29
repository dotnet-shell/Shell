using Dotnet.Script.Core;
using Dotnet.Script.DependencyModel.Context;
using Dotnet.Script.DependencyModel.Logging;
using Dotnet.Script.DependencyModel.NuGet;
using Dotnet.Shell.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Dotnet.Shell.Logic.Compilation
{
    /// <summary>
    /// This class integrates dotnet-shell with Dotnet Script.
    /// It is based on a simplified version of https://github.com/filipw/dotnet-script/blob/master/src/Dotnet.Script.Core/Interactive/InteractiveRunner.cs
    /// </summary>
    internal sealed class InteractiveRunner
    {
        private ScriptState<object> _scriptState;
        private ScriptOptions _scriptOptions;

        private readonly InteractiveScriptGlobals _globals;
        private readonly string[] _packageSources = Array.Empty<string>();

        private readonly Logger _logger;
        private readonly ScriptCompiler _scriptCompiler;
        private readonly ScriptConsole _scriptConsole;

        public InteractiveRunner(ErrorDisplay errorDisplay)
        {
            var logFactory = CreateLogFactory("WARNING", errorDisplay);

            _logger = logFactory.CreateLogger<InteractiveRunner>();
            _scriptConsole = ScriptConsole.Default;
            _globals = new InteractiveScriptGlobals(_scriptConsole.Out, CSharpObjectFormatter.Instance);
            _scriptCompiler = new ScriptCompiler(logFactory, false);
        }

        public ImmutableDictionary<string, object> ScriptVariables
        {
            get
            {
                var vars = new Dictionary<string, object>();

                foreach (var variable in _scriptState.Variables)
                {
                    if (!vars.ContainsKey(variable.Name))
                    {
                        vars.Add(variable.Name, variable.Value);
                    }
                }

                return vars.ToImmutableDictionary();
            }
        }

       

        public async Task<object> ExecuteAsync(string input, string workingDirectory)
        {
            if (_scriptState == null)
            {
                var sourceText = SourceText.From(input);
                var context = new ScriptContext(sourceText, workingDirectory, Enumerable.Empty<string>(), scriptMode: ScriptMode.REPL, packageSources: _packageSources);
                
                await RunFirstScriptAsync(context);
            }
            else
            {
                if (input.StartsWith("#r ") || input.StartsWith(ShellExecutor.LOAD_MARKER))
                {
                    var lineRuntimeDependencies = _scriptCompiler.RuntimeDependencyResolver.GetDependenciesForCode(workingDirectory, ScriptMode.REPL, _packageSources, input).ToArray();
                    var lineDependencies = lineRuntimeDependencies.SelectMany(rtd => rtd.Assemblies).Distinct();

                    var scriptMap = lineRuntimeDependencies.ToDictionary(rdt => rdt.Name, rdt => rdt.Scripts);
                    if (scriptMap.Count > 0)
                    {
                        _scriptOptions =
                            _scriptOptions.WithSourceResolver(
                                new NuGetSourceReferenceResolver(
                                    new SourceFileResolver(ImmutableArray<string>.Empty, workingDirectory), scriptMap));
                    }
                    foreach (var runtimeDependency in lineDependencies)
                    {
                        _logger.Debug("Adding reference to a runtime dependency => " + runtimeDependency);
                        _scriptOptions = _scriptOptions.AddReferences(MetadataReference.CreateFromFile(runtimeDependency.Path));
                    }
                }
                _scriptState = await _scriptState.ContinueWithAsync(input, _scriptOptions);
            }

            return _scriptState.ReturnValue;
        }

        private async Task RunFirstScriptAsync(ScriptContext scriptContext)
        {
            foreach (var arg in scriptContext.Args)
            {
                _globals.Args.Add(arg);
            }

            var compilationContext = _scriptCompiler.CreateCompilationContext<object, InteractiveScriptGlobals>(scriptContext);
            _scriptConsole.WriteDiagnostics(compilationContext.Warnings, compilationContext.Errors);

            if (compilationContext.Errors.Length >0)
            {
                throw new CompilationErrorException("Script compilation failed due to one or more errors.", compilationContext.Errors.ToImmutableArray());
            }

            _scriptState = await compilationContext.Script.RunAsync(_globals, ex => true).ConfigureAwait(false);
            _scriptOptions = compilationContext.ScriptOptions;
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