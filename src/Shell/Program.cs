using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime;
using Mono.Unix;
using Mono.Unix.Native;
using System.Runtime.InteropServices;
using CommandLine;
using System.Diagnostics;
using Dotnet.Shell.Logic.Console;
using Dotnet.Shell.UI;
using Dotnet.Shell.Logic;
using Dotnet.Shell.UI.Enhanced;
using Dotnet.Shell.Logic.Compilation;
using Dotnet.Shell.UI.Standard;
using Dotnet.Shell.Logic.Execution;
using Dotnet.Shell.API;

namespace Dotnet.Shell
{
    class Program
    {
        private static readonly IConsole consoleInterface = new DotNetConsole();
        private static readonly ErrorDisplay errorHelper = new(consoleInterface);

        static async Task Main(string[] args)
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nsh");
            if (Directory.Exists(configDir))
            {
                ProfileOptimization.SetProfileRoot(Path.Combine(configDir, "profiles"));
            }
            ProfileOptimization.StartProfile("Startup.Profile");

            Parser.Default.ParseArguments<Settings>(args).WithParsed<Settings>(o => Settings.Default = o);

            if (Settings.Default == null)
            {
                return;
            }
            Settings.Default.AddComplexDefaults();

            if (Settings.Default.EarlyDebuggerAttach)
            {
                Console.WriteLine("Now connect your debugger to "+ Environment.ProcessId);
                while (!Debugger.IsAttached)
                {
                    Console.Write(".");
                    await Task.Delay(1000);
                }
                Console.Clear();
            }

            if (!Settings.Default.DontRunWizard && !FirstRunWizard.WizardUI.Run())
            {
                return;
            }

            if (Settings.Default.HistoryMode)
            {
                ProfileOptimization.StartProfile("History.Profile");
                try
                {
                    var historyTask = OS.GetOSHistoryAsync();
                    var box = new HistoryBox(consoleInterface);
                    var result = await box.RunInterfaceAsync((await historyTask).ToList());

                    var response = API.HistoryAPI.SearchResultAsync(result, Settings.Default.APIPort, Settings.Default.Token);

                    Console.Clear();

                    await response;
                }
                catch (Exception ex)
                {
                    errorHelper.PrettyException(ex);
                    await Task.Delay(5 * 1000);
                }
                return;
            }
            else
            {
                var fileArguments = args.Where(x => x.EndsWith(".csx") || x.EndsWith(".cs") || x.EndsWith(Dotnet.Shell.API.Shell.DefaultScriptExtension));

                if (fileArguments.Any())
                {
                    ProfileOptimization.StartProfile("Script.Profile");
                    var script = fileArguments.First();
                    List<string> scriptArgs = new();
                    bool startCollecting = false;
                    for (int x = 0; x < args.Length; x++)
                    {
                        if (args[x] == script)
                        {
                            startCollecting = true;
                            continue;
                        }
                        else if (startCollecting)
                        {
                            scriptArgs.Add(args[x]);
                        }
                    }

                    Executer executor = await Executer.GetDefaultExecuterAsync(errorHelper);
                    executor.Args.AddRange(scriptArgs);

                    await executor.ExecuteFileAsync(script);
                }
                else
                {
                    ProfileOptimization.StartProfile("Interactive.Profile");
                    await StartInteractiveModeAsync(Settings.Default.UX);
                }     
            }
        }

        private static async Task StartInteractiveModeAsync(UserExperience ux)
        {
            using (var cts = new CancellationTokenSource())
            {
                var executor = await Executer.GetDefaultExecuterAsync(errorHelper);

                var historyToWrite = new List<HistoryItem>();

                executor.Shell.CommandHandlers.Add((cmd) =>
                {
                    historyToWrite.Add(new HistoryItem(cmd, DateTime.UtcNow));
                    return cmd;
                });

                var historyWritingTask = Task.Run(async () => {

                    while (!cts.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(60), cts.Token);

                        if (!cts.IsCancellationRequested)
                        {
                            var localCopy = historyToWrite;

                            Interlocked.Exchange(ref historyToWrite, new List<HistoryItem>());

                            if (localCopy.Count != 0)
                            {
                                await OS.WriteHistoryAsync(localCopy);
                            }
                        }
                    }
                }, cts.Token);

                Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs args) => {
                    args.Cancel = true;
                    executor?.Shell?.ForegroundProcess?.SignalTerminate();
                };

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var ctrlZHandler = new Thread(delegate ()
                    {
                        try
                        {
                            while (true)
                            {
                                var waitForCtrlZ = new UnixSignal(Signum.SIGTSTP);

                                Task.Run(() => waitForCtrlZ.WaitOne(), cts.Token).Wait(cts.Token);

                                if (!cts.IsCancellationRequested)
                                {
                                    Console.WriteLine();
                                    executor?.Shell?.ForegroundProcess?.SignalSuspend();
                                }
                            }
                        }
                        catch
                        {

                        }
                    });
                    ctrlZHandler.Start();
                }

                if (FindAndLoadCore(out string coreScript))
                {
                    try
                    {
                        await executor.ExecuteAsync(coreScript);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("An error occured loading core.nsh");
                        errorHelper.PrettyException(ex, coreScript);
                    }
                }

                var console = new ConsoleImproved(consoleInterface, executor.Shell);

                var searchFunction = ux == UserExperience.Classic ? HistorySearch.OnSearchHistory(consoleInterface, executor.Shell) :
                                     ux == UserExperience.Enhanced ? HistoryBox.OnSearchHistory(consoleInterface) : HistoryBox.OnSearchHistoryTmux();

                var suggestor = new Logic.Suggestions.Suggestions(executor.Shell);

                console.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.R, ConsoleModifiers.Control), searchFunction);
                console.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Tab), suggestor.OnTabSuggestCmdAsync);

                while (true) // main command loop
                {
                    console.DisplayPrompt();
                    string input;

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                    await executor.Shell.HistoryLoadedTask;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks

                    try
                    {
                        input = await console.GetCommandAsync();
                    }
                    catch (Exception ex)
                    {
                        errorHelper.PrettyException(ex);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        if (input == "#reset")
                        {
                            executor = await Executer.GetDefaultExecuterAsync(errorHelper);
                            executor.Shell.CommandHandlers.Add((cmd) =>
                            {
                                historyToWrite.Add(new HistoryItem(cmd, DateTime.UtcNow));
                                return cmd;
                            });
                            await executor.ExecuteAsync(coreScript);
                            continue;
                        }

                        try
                        {
                            input = executor.Shell.TryReplaceWithCSAlias(input);
                            await executor.ExecuteAsync(input);
                        }
                        catch (ExitException)
                        {
                            cts.Cancel();
                            await OS.WriteHistoryAsync(historyToWrite);
                            break;
                        }
                        catch (PreProcessorSyntaxException ex)
                        {
                            errorHelper.PrettyException(ex);
                        }
                        catch (Exception ex)
                        {
                            errorHelper.PrettyException(ex, input);
                        }
                    }
                }
            }
        }

        private static bool FindAndLoadCore(out string content)
        {
            if (File.Exists(Settings.Default.ProfileScriptPath))
            {
                try
                {
                    content = File.ReadAllText(Settings.Default.ProfileScriptPath);
                    return true;
                }
                catch
                {
                }
            }

            content = null;
            return false;
        }
    }
}
