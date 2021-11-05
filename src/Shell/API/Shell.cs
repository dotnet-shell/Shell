using Dotnet.Shell.Logic.Execution;
using Dotnet.Shell.Logic.Suggestions;
using Dotnet.Shell.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("UnitTests")]

namespace Dotnet.Shell.API
{
    /// <summary>
    /// The Shell class is the primary place user can call to get access to shell functionality.
    /// </summary>
    public class Shell
    {
        /// <summary>
        /// Gets the base path. On Linux this is / on Windows C:\
        /// </summary>
        /// <value>
        /// The base path.
        /// </value>
        public static string BasePath
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return "/";
                }
                else
                {
                    return "C:\\";
                }
            }
        }

        /// <summary>
        /// The default extension for scripts run through the scripting engine
        /// </summary>
        public const string DefaultScriptExtension = ".nsh";

        /// <summary>Gets or sets the working directory.</summary>
        /// <value>The working directory.</value>
        public string WorkingDirectory { get; internal set; } = Environment.CurrentDirectory;

        /// <summary>Gets the paths.</summary>
        /// <value>The paths.</value>
        public List<string> Paths { get; } = new List<string>();

        /// <summary>
        /// Gets or sets the last exit code.
        /// </summary>
        /// <value>
        /// The last exit code.
        /// </value>
        public int LastExitCode { get; internal set; } = 0;

        /// <summary>
        /// Gets or sets the function used to return the prompt string.
        /// This function is generally set by the user to customize the prompt
        /// </summary>
        /// <value>
        /// The prompt function
        /// </value>
        public Func<ColorString> Prompt { get; set; }

        /// <summary>
        /// Gets or sets the command handler. This is called after the user has hit Enter and can be used to
        /// replace what was entered
        /// </summary>
        /// <value>
        /// The command handler.
        /// </value>
        public List<Func<string, string>> CommandHandlers { get; } = new List<Func<string, string>>();

        /// <summary>
        /// A list of autocompletion handlers which can be used by the user to extend autocompletion results
        /// </summary>
        /// <value>
        /// The autocompletion handlers.
        /// </value>
        public List<Func<string, int, Task<IEnumerable<Suggestion>>>> AutoCompletionHandlers { get; } = new List<Func<string, int, Task<IEnumerable<Suggestion>>>>();

        /// <summary>
        /// Gets the executing program location.
        /// </summary>
        /// <value>
        /// The assembly location.
        /// </value>
        public static string AssemblyLocation => Assembly.GetEntryAssembly().Location;

        /// <summary>
        /// The shell history
        /// </summary>
        public virtual List<HistoryItem> History { get; } = new List<HistoryItem>();

        /// <summary>
        /// Gets the home directory.
        /// </summary>
        /// <value>
        /// The home directory.
        /// </value>
        public string HomeDirectory { get; internal set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        /// <summary>
        /// This function can be called to render a pretty error message.
        /// </summary>
        public Action<string> Error { get; set; }

        /// <summary>
        /// This function can be called to load a .NET DLL into memory
        /// </summary>
        public Func<string, Task> LoadAssemblyFromFileAsync { get; internal set; }

        /// <summary>
        /// This function can be called to load script into memory
        /// </summary>
        public Func<string, Task> LoadScriptFromFileAsync { get; internal set; }

        /// <summary>
        /// Gets the current set of background processes.
        /// </summary>
        /// <value>
        /// The background processes.
        /// </value>
        public ProcessEx[] BackgroundProcesses => backgroundProcesses.ToArray();

        /// <summary>
        /// The foreground process
        /// </summary>
        public ProcessEx ForegroundProcess = null;

        /// <summary>
        /// The last executed process
        /// </summary>
        public ProcessEx LastExecutedProcess = null;

        /// <summary>
        /// The history loaded task, wait on this to ensure all history elements have been loaded into memory
        /// </summary>
        public Task HistoryLoadedTask = Task.CompletedTask;

        internal List<ProcessEx> backgroundProcesses = new();
        internal Dictionary<string, string> csAliases = new();
        internal Dictionary<string, string> cmdAliases = new();
        internal Func<Dictionary<string, string>> EnvironmentVariables = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Shell"/> class.
        /// </summary>
        /// <param name="standardInput">The open standard input.</param>
        /// <param name="standardOutput">The open standard output.</param>
        /// <param name="standardError">The open standard error.</param>
        public Shell(bool loadHistory = true)
        {
            Prompt = () => "# > ";

            if (loadHistory)
            {
                HistoryLoadedTask = Task.Run(async () => History.AddRange(await OS.GetOSHistoryAsync()));
            }
        }

        internal void SetForegroundProcess(ProcessEx procEx)
        {
            if (procEx == null)
            {
                LastExecutedProcess = ForegroundProcess;
            }
            ForegroundProcess = procEx;
        }

        /// <summary>
        /// Adds a shell alias for C# commands
        /// An example of this is Shell.AddCSAlias("echo", "Console.WriteLine(\"{0}\");");
        /// </summary>
        /// <param name="command">The user entered command.</param>
        /// <param name="replacement">The C# replacement.</param>
        public void AddCSAlias(string command, string replacement)
        {
            SafeDictionaryAdd(command, replacement, ref csAliases, "C# alias ({0}) already exists, ignoring. Remove this alias to stop this warning");
        }

        /// <summary>
        /// Removes a C# alias.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns></returns>
        public void RemoveCSAlias(string command)
        {
            if (csAliases.ContainsKey(command))
            {
                csAliases.Remove(command);
            }
        }

        /// <summary>
        /// Adds a command alias
        /// An example of this is Shell.AddCmdAlias("ls", "ls --color=auto ");
        /// </summary>
        /// <param name="command">The user entered command.</param>
        /// <param name="replacement">The C# replacement.</param>
        public void AddCmdAlias(string command, string replacement)
        {
            SafeDictionaryAdd(command, replacement, ref cmdAliases, "Command alias ({0}) already exists, ignoring. Remove this alias to stop this warning");
        }

        /// <summary>
        /// Removes a command alias.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns></returns>
        public void RemoveCmdAlias(string command)
        {
            if (cmdAliases.ContainsKey(command))
            {
                cmdAliases.Remove(command);
            }
        }

        private void SafeDictionaryAdd(string key, string value, ref Dictionary<string, string> dict, string error)
        {
            if (dict.ContainsKey(key))
            {
                Error?.Invoke(string.Format(error, key));
                return;
            }
            dict.Add(key, value);
        }

        /// <summary>
        /// API call to changes the working directory.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public void ChangeDir(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                WorkingDirectory = HomeDirectory;
            }
            else
            {
                location = location.Replace("~", HomeDirectory);
                var newPath = Path.Combine(WorkingDirectory, location);

                if (Directory.Exists(newPath))
                {
                    WorkingDirectory = Path.GetFullPath(newPath);
                    Environment.CurrentDirectory = WorkingDirectory;
                }
                else
                {
                    throw new DirectoryNotFoundException(location + ": No such file or directory");
                }
            }
        }

        /// <summary>
        /// Converts the a file path to absolute based on the current working directory
        /// This is a public API and is used in #load
        /// </summary>
        /// <param name="file">The file path</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException">The file requested: " + file + " does not exist and cannot be loaded</exception>
        public string ConvertPathToAbsolute(string file)
        {
            if (!File.Exists(file))
            {
                file = file.Replace("~", HomeDirectory);

                var inWorkingDir = Path.Combine(WorkingDirectory, file);
                if (File.Exists(inWorkingDir))
                {
                    file = inWorkingDir;
                }
            }

            var fi = new FileInfo(file);

            if (!fi.Exists)
            {
                throw new FileNotFoundException("The file requested: " + file + " does not exist and cannot be loaded");
            }

            return fi.FullName;
        }

        internal void AddToBackgroundProcesses(ProcessEx p)
        {
            Console.WriteLine("Process suspended, id=" + p.Process.Id);
            backgroundProcesses.Add(p);
        }

        internal void RemoveFromBackgroundProcesses(ProcessEx processEx)
        {
            backgroundProcesses.Remove(processEx);
        }

        internal string GetRandomAssemblyName()
        {
            var dir = Path.Combine(HomeDirectory, DefaultScriptExtension, "assemblies");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return Path.Combine(dir, DateTime.UtcNow.Ticks + ".dll");
        }

        private static void SplitCommandLine(string cmdline, out string exe, out string args)
        {
            cmdline = cmdline.TrimStart();
            if (cmdline.StartsWith("'") || cmdline.StartsWith("\\"))
            {
                throw new NotImplementedException();
            }
            else if (cmdline.Contains(" "))
            {
                exe = cmdline.Split(" ").First();
                args = cmdline.Remove(0, exe.Length + 1); // +1 for the space
            }
            else
            {
                args = string.Empty;
                exe = cmdline;
            }
        }

        private static bool AliasReplace(string originalLine, string exe, string args, Dictionary<string, string> aliases, out string replacement, bool useFormatStr)
        {
            if (aliases != null && aliases.ContainsKey(originalLine))
            {
                if (useFormatStr)
                {
                    replacement = string.Format(aliases[originalLine], string.Empty);
                }
                else
                {
                    replacement = aliases[originalLine];
                }
                return true;
            }
            else if (aliases != null && aliases.ContainsKey(exe))
            {
                if (useFormatStr)
                {
                    replacement = string.Format(aliases[exe], args);
                }
                else
                {
                    replacement = aliases[exe] + args;
                }
                return true;
            }
            else
            {
                replacement = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// Tries to replace the string with an alias which is a piece of C#
        /// </summary>
        /// <param name="input">The user input command</param>
        /// <returns>Command with possible alias replacement</returns>
        public string TryReplaceWithCSAlias(string input)
        {
            SplitCommandLine(input, out string exe, out string args);

            if (AliasReplace(input, exe, args, csAliases, out string replacement, true))
            {
                input = replacement;
            }

            return input;
        }

        /// <summary>
        /// Executes the an OS command asynchronously.
        /// </summary>
        /// <param name="input">The command line.</param>
        /// <param name="r">The redirection conditions, if any</param>
        /// <returns>Task</returns>
        public Task<ProcessEx> ExecuteAsync(string input, object r = null)
        {
            Redirection redirection = r == null ? Redirection.None : (Redirection)r;
            SplitCommandLine(input, out string exe, out string args);

            if (AliasReplace(input, exe, args, cmdAliases, out string replacement, false))
            {
                input = replacement;
            }

            return Task.Run(() => OS.Exec(input, this, redirection).WaitTillExit(this));
        }

        /// <summary>
        /// Executes the an OS command synchronously.
        /// </summary>
        /// <param name="input">The command line.</param>
        /// <param name="r">The redirection conditions, if any</param>
        /// <returns>Task</returns>
        public ProcessEx Execute(string input, object r = null)
        {
            Redirection redirection = r == null ? Redirection.None : (Redirection)r;
            var task = Task.Run(async () => await ExecuteAsync(input, redirection));
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            task.Wait();
            return task.Result;
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }
    }
}
