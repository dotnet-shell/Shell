using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dotnet.Shell.Logic.Execution;
using Dotnet.Shell.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.Scripting;

namespace Dotnet.Shell.Logic.Compilation
{
    internal class Executer
    {
        private readonly ErrorDisplay errorHelper;
        private readonly ConcurrentQueue<string> runOnCompletion = new ConcurrentQueue<string>();
        private readonly SourceProcessor nshProcessor = new SourceProcessor();
        private InteractiveRunner runner;

        public Dotnet.Shell.API.Shell Shell => this.runner.ScriptVariables["Shell"] as Dotnet.Shell.API.Shell;

        public List<string> Args => this.runner.ScriptVariables["Args"] as List<string>;

        public static async Task<Executer> GetDefaultExecuterAsync(ErrorDisplay errorHelper)
        {
            var executer = new Executer(errorHelper);
            await executer.CreateDefaultShellAsync();

            executer.Shell.EnvironmentVariables = () =>
            {
                var ret = new Dictionary<string, string>();
                foreach (var variable in executer.runner.ScriptVariables)
                {
                    if (variable.Value != null)
                    {
                        ret.Add(variable.Key, variable.Value.ToString());
                    }
                }

                var systemPath = Environment.GetEnvironmentVariable("PATH");

                var pathSeperator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";
                var key = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Path" : "PATH";
                ret.Add(key, systemPath + pathSeperator + string.Join(pathSeperator, executer.Shell.Paths));

                return ret;
            };
            executer.Shell.Error = errorHelper.PrettyError;
            executer.Shell.LoadAssemblyFromFileAsync = executer.LoadAssemblyFromFileAsync;
            executer.Shell.LoadScriptFromFileAsync = async (file) =>
            {
                if (file.EndsWith(Dotnet.Shell.API.Shell.DefaultScriptExtension))
                {
                    var result = await executer.nshProcessor.ProcessAsync(File.ReadAllText(file));

                    if (Settings.Default.ShowPreProcessorOutput)
                    {
                        errorHelper.PrettyInfo(result);
                    }

                    executer.runOnCompletion.Enqueue(result);
                }
                else
                {
                    executer.runOnCompletion.Enqueue("#load \""+file+"\"");
                }
            };

            return executer;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Executer"/> class.
        /// Created via the static initializer GetExecuter due to use of async in possible construction
        /// </summary>
        /// <param name="errorHelper">The error helper.</param>
        private Executer(ErrorDisplay errorHelper)
        {
            this.errorHelper = errorHelper;
            this.runner = new InteractiveRunner(errorHelper);
        }

        public Task LoadAssemblyFromFileAsync(string dll)
        {
            if (!File.Exists(dll))
            {
                throw new FileNotFoundException(dll);
            }

            runOnCompletion.Enqueue("#r \"" + dll + "\"");

            return Task.CompletedTask;
        }

        private async Task CreateDefaultShellAsync()
        {
            var script = @"
#r ""dotnet-shell.dll""
using Dotnet.Shell;
using Dotnet.Shell.UI;
using Dotnet.Shell.API;
using Dotnet.Shell.Logic.Execution;
using System;
using System.Collections.Generic;
using System.Data;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CSharp;
var Args = new List<string>();
";

            foreach (var u in Settings.Default.AdditionalUsings)
            {
                script += "using " + u + ";"+Environment.NewLine;
            }
            script += "var Shell = new Shell();";

            await this.runner.ExecuteAsync(script, Environment.CurrentDirectory);
        }

        public async Task ExecuteAsync(string line, int depth = 0, bool preprocess = true)
        {
            try
            {
                if (preprocess)
                {
                    line = await nshProcessor.ProcessAsync(line);
                    if (Settings.Default.ShowPreProcessorOutput)
                    {
                        errorHelper.PrettyInfo(line);
                    }
                }

                const string LOAD = "#load ";
                if (line.StartsWith(LOAD) && line.EndsWith(".cs\""))
                {
                    // we want to #load some CS, we can do this!
                    await CompileNewAssemblyAsync(await File.ReadAllTextAsync( line.Remove(0, LOAD.Length).Trim('"')));
                }
                else
                {
                    if (depth == 0 && line.EndsWith(";")/* && !new Regex(@"^.+\w+.+=.+;$").IsMatch(line)*/)
                    {
                        line = line.TrimEnd(';');
                    }

                    var ret = await this.runner.ExecuteAsync(line, Shell.WorkingDirectory);

                    if (ret != null && ret is not ProcessEx)
                    {
                        System.Console.WriteLine(CSharpObjectFormatter.Instance.FormatObject(ret));
                    }
                }
            }
            catch (CompilationErrorException ex)
            {
                // TODO documentation here!
                if (ex.Message.Contains("error CS1002: ; expected") && depth == 0)
                {
                    // readd the char we removed
                    await ExecuteAsync(line + ';', depth + 1, false);
                }
                else if (ex.Message.Contains("CS0201") && depth == 0)
                {
                    await ExecuteAsync(line.TrimEnd(';'), depth+ 1, false);
                }
                else
                {
                    throw;
                }
            }

            while (!runOnCompletion.IsEmpty && depth == 0)
            {
                if (runOnCompletion.TryDequeue(out string script))
                {
                    await ExecuteAsync(script, 0, false);
                }
            }
        }

        public async Task ExecuteFileAsync(string file)
        {
            try
            {
                // this will queue up a execution 
                await Shell.LoadScriptFromFileAsync(file);

                while (!runOnCompletion.IsEmpty)
                {
                    if (runOnCompletion.TryDequeue(out string script))
                    {
                        await ExecuteAsync(script, 0, false);
                    }
                }
            }
            catch (PreProcessorSyntaxException ex)
            {
                errorHelper.PrettyException(ex);
            }
            catch (Exception ex)
            {
                errorHelper.PrettyException(ex, file, file);
            }
        }

        private List<MetadataReference> GetAllLoadedAssemblies()
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

        private async Task CompileNewAssemblyAsync(string text)
        {
            // define source code, then parse it (to the type used for compilation)
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(text);

            // define other necessary objects for compilation
            string assemblyName = Shell.GetRandomAssemblyName();

            // analyse and generate IL code from syntax tree
            CSharpCompilation compilation = CSharpCompilation.Create(
                Path.GetFileName(assemblyName),
                syntaxTrees: new[] { syntaxTree },
                references: GetAllLoadedAssemblies(),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                // write IL code into memory
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // handle exceptions
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        await System.Console.Error.WriteLineAsync(string.Format("{0}: {1}", diagnostic.Id, diagnostic.GetMessage()));
                    }
                }
                else
                {
                    // load this 'virtual' DLL so that we can use
                    ms.Seek(0, SeekOrigin.Begin);

                    var assemblyBinary = ms.ToArray();
                    await File.WriteAllBytesAsync(assemblyName, assemblyBinary);

                    await LoadAssemblyFromFileAsync(assemblyName);
                }
            }
        }
    }
}
