using Dotnet.Shell.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text;

namespace Dotnet.Shell.Logic.Execution
{
    /// <summary>
    /// Flags to determine which stream has been redirected
    /// </summary>
    [Flags]
    public enum Redirection
    {
        /// <summary>
        /// No stream redirection
        /// </summary>
        None = 0,
        /// <summary>
        /// StdOut redirection
        /// </summary>
        Out = 1,
        /// <summary>
        /// StdErr redirection
        /// </summary>
        Err = 2
    }

    /// <summary>
    /// This class implements OS actions such as Execution
    /// </summary>
    public class OS
    {
        /// <summary>
        /// Executes the specified cmdline.
        /// </summary>
        /// <param name="cmdline">The cmdline.</param>
        /// <param name="shellObj">The shell object.</param>
        /// <param name="redirectionObj">The redirection object.</param>
        /// <returns>Process</returns>
        public static ProcessEx Exec(string cmdline, object shellObj = null, Object redirectionObj = null)
        {
            Dotnet.Shell.API.Shell shell = shellObj as Dotnet.Shell.API.Shell;
            Redirection redirection = redirectionObj == null ? Redirection.None : (Redirection)redirectionObj;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Settings.Default.SubShellArgumentsFormat.Contains("-Encoded"))
            {
                cmdline = Convert.ToBase64String(Encoding.Unicode.GetBytes(cmdline));
            }

            var proc = new Process();
            proc.StartInfo.RedirectStandardError = redirection.HasFlag(Redirection.Err);
            proc.StartInfo.RedirectStandardOutput = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || redirection.HasFlag(Redirection.Out);
            proc.StartInfo.RedirectStandardInput = false;
            proc.StartInfo.WorkingDirectory = shell != null ? shell.WorkingDirectory : Environment.CurrentDirectory;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.FileName = Settings.Default.SubShell;
            proc.StartInfo.Arguments = string.Format(Settings.Default.SubShellArgumentsFormat, cmdline.Replace("\"", "\\\""));
            proc.StartInfo.UseShellExecute = false;

            if (shell != null)
            {
                // add any variables that have been created
                foreach (var kvp in shell.EnvironmentVariables())
                {
                    if (!proc.StartInfo.EnvironmentVariables.ContainsKey(kvp.Key))
                    {
                        proc.StartInfo.EnvironmentVariables.Add(kvp.Key, kvp.Value);
                    }
                    else if (kvp.Key.ToLower() == "path")
                    {
                        proc.StartInfo.EnvironmentVariables.Remove(kvp.Key);
                        proc.StartInfo.EnvironmentVariables.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            proc.Start();
            var procEx = new ProcessEx(proc);

            // Windows handles stdout redirection differently, to work around this we copy to console if
            // we have no redirection otherwise we copy to an internal stream
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (redirection.HasFlag(Redirection.Out))
                {
                    // use internal stream
                    _ = proc.StandardOutput.BaseStream.CopyToAsync(procEx.WindowsStdOut);
                }
                else
                {
                    // copy straight to the console
                    _ = proc.StandardOutput.BaseStream.CopyToAsync(System.Console.OpenStandardOutput());
                }
            }

            return procEx;
        }

        /// <summary>
        /// Gets the OS command history.
        /// </summary>
        /// <returns>History</returns>
        public static async Task<IEnumerable<HistoryItem>> GetOSHistoryAsync()
        {
            var history = new ConcurrentBag<HistoryItem>();
            var tasksToWaitOn = new List<Task>();

            foreach (var additionalHistoryFile in Settings.Default.AdditionalHistoryFiles)
            {
                tasksToWaitOn.Add(Task.Run(async () =>
               {
                   if (File.Exists(additionalHistoryFile))
                   {
                       var lines = await File.ReadAllLinesAsync(additionalHistoryFile);
                       for (int offset = 0; offset < lines.Length; offset++)
                       {
                           if (!string.IsNullOrWhiteSpace(lines[offset]))
                           {
                               history.Add(new HistoryItem(lines[offset], offset));
                           }
                       }
                   }
               }));
            }

            tasksToWaitOn.Add(Task.Run(async () => {
                if (File.Exists(Settings.Default.HistoryFile))
                {
                    var jsonLines = await File.ReadAllLinesAsync(Settings.Default.HistoryFile);

                    Parallel.ForEach<string>(jsonLines, item => {
                        if (!string.IsNullOrWhiteSpace(item))
                        {
                            history.Add(JsonConvert.DeserializeObject<HistoryItem>(item));
                        }
                    });
                }
            }));

            await Task.WhenAll(tasksToWaitOn);

            return history.OrderBy(o => o.TimeRun).ToList();
        }

        /// <summary>
        /// Writes the history asynchronous to the configured history file
        /// </summary>
        /// <param name="history">The history.</param>
        public static async Task WriteHistoryAsync(IEnumerable<HistoryItem> history)
        {
            string baseDir = Path.GetDirectoryName(Settings.Default.HistoryFile);

            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            var json = history.ToList().ConvertAll<string>(x => x.Serialize());

            await File.AppendAllLinesAsync(Settings.Default.HistoryFile, json);
        }
    }
}
