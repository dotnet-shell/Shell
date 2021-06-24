using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Dotnet.Shell.Logic
{
    /// <summary>
    /// The User Experience to use when console rendering
    /// </summary>
    public enum UserExperience
    {
        /// <summary>
        /// The classic mode - similar to Bash
        /// </summary>
        Classic,
        /// <summary>
        /// Enhanced mode with improved history
        /// </summary>
        Enhanced,
        /// <summary>
        /// The TMux enhanced version which uses Tmux popup functionality
        /// </summary>
        TmuxEnhanced
    }

    /// <summary>
    /// This defined all the global settings these can either be set on the command line or via a script
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// The default settings used by dotnet-shell
        /// </summary>
        public static Settings Default = new();

        public void AddComplexDefaults()
        {
            if (!AdditionalHistoryFiles.Any())
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    this.AdditionalHistoryFiles = new List<string>() { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bash_history") };
            }
                else
                {
                    this.AdditionalHistoryFiles = new List<string>() { Environment.ExpandEnvironmentVariables(@"%userprofile%\AppData\Roaming\Microsoft\Windows\PowerShell\PSReadline\ConsoleHost_history.txt") };
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Settings"/> is verbose.
        /// </summary>
        /// <value>
        ///   <c>true</c> if verbose; otherwise, <c>false</c>.
        /// </value>
        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.", Default = false)]
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [early debugger attach].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [early debugger attach]; otherwise, <c>false</c>.
        /// </value>
        [Option("earlyDebuggerAttach", Required = false, HelpText = "Enables early debugging for initialization related issues", Default = false)]
        public bool EarlyDebuggerAttach { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [show pre processor output].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [show pre processor output]; otherwise, <c>false</c>.
        /// </value>
        [Option("showPreProcessorOutput", Required = false, HelpText = "Outputs preprocessed scripts and commands to StdOut prior to execution", Default = false)]
        public bool ShowPreProcessorOutput { get; set; }

        /// <summary>
        /// Gets or sets the token.
        /// </summary>
        /// <value>
        /// The token.
        /// </value>
        [Option("token", Required = false, HelpText = "Token shared between client and server instances", Hidden = true, SetName = "history")]
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the API port.
        /// </summary>
        /// <value>
        /// The API port.
        /// </value>
        [Option("apiport", Required = false, HelpText = "The port number of the API interface", Hidden = true, SetName = "history")]
        public int APIPort { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [history mode].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [history mode]; otherwise, <c>false</c>.
        /// </value>
        [Option("history", Required = false, HelpText = "Starts the shell in history display mode", Hidden = true, Default = false, SetName = "history")]
        public bool HistoryMode { get; set; }

        /// <summary>
        /// Gets or sets the user experience mode.
        /// </summary>
        /// <value>
        /// The ux.
        /// </value>
        [Option('x', "ux", Required = false, HelpText = "The user experience mode the shell starts in", Default = UserExperience.Enhanced)]
        public UserExperience UX { get; set; }

        /// <summary>
        /// Gets the profile script path.
        /// </summary>
        /// <value>
        /// The profile script path.
        /// </value>
        [Option("profile", Required = false, HelpText = "The path to the personal initialization script file (core.nsh)")]
        public string ProfileScriptPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nsh", "core.nsh");

        /// <summary>
        /// Gets the sub shell.
        /// </summary>
        /// <value>
        /// The sub shell.
        /// </value>
        [Option('s', "subShell", Required = false, HelpText = "Path to the sub shell to invoke commands with")]
        public string SubShell { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell.exe" : "/bin/bash";

        /// <summary>
        /// Gets the sub shell arguments format.
        /// </summary>
        /// <value>
        /// The sub shell arguments format.
        /// </value>
        [Option('a', "subShellArgs", Required = false, HelpText = "Arguments to the provided to the SubShell, this MUST include the format specifier {0}")]
        public string SubShellArgumentsFormat { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-NoProfile -ExecutionPolicy unrestricted -Command {0}" : "-c \"{0}\"";

        /// <summary>
        /// Gets the additional usings.
        /// </summary>
        /// <value>
        /// The additional usings.
        /// </value>
        [Option('u', "using", Required = false, HelpText = "Additional 'using' statements to include")]
        public IEnumerable<string> AdditionalUsings { get; set; } = new List<string>();

        /// <summary>
        /// Gets the popup command.
        /// </summary>
        /// <value>
        /// The popup command.
        /// </value>
        [Option("popupCmd", Required = false, HelpText = "Command to run to raise a system popup window, must include {0} format specifier for the dotnet-shell command", Default = "tmux popup -KER '{0}' -x 60 -y 0 -w 60% -h 100%")]
        public string PopupCommand { get; set; }

        /// <summary>
        /// Gets the history popup command.
        /// </summary>
        /// <value>
        /// The history popup command.
        /// </value>
        [Option("historyCmd", Required = false, HelpText = "dotnet-shell command line to execute when the history subprocess. Must include {0} format specifier for DLL location, {1} for port and {2} for token parameters", Default = "dotnet {0} --history --apiport {1} --token {2}")]
        public string HistoryPopupCommand { get; set; }

        /// <summary>
        /// Adds one or more history file
        /// History files are interpreted as one command per line
        /// </summary>
        /// <value>
        /// The history files.
        /// </value>
        [Option("additionalHistory", Required = false, HelpText = "Path to additional OS specific history files")]
        public IEnumerable<string> AdditionalHistoryFiles { get; set; } = new List<string>();

        [Option("historyPath", Required = false, HelpText = "Path to CSX history file")]
        public string HistoryFile { get; set; } = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Dotnet.Shell.API.Shell.DefaultScriptExtension), "history");

        [Option("nowizard", Required = false, HelpText = "Do not try and run the initial set up wizard", Default = false)]
        public bool DontRunWizard { get; set; }

    }
}
