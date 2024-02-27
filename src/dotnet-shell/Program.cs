using CommandLine;
using Dotnet.Shell.API;
using Dotnet.Shell.API.Helpers;
using Dotnet.Shell.Logic;
using Dotnet.Shell.Logic.Compilation;
using Dotnet.Shell.Logic.Console;
using Dotnet.Shell.Logic.Execution;
using Dotnet.Shell.UI;
using Dotnet.Shell.UI.Enhanced;
using Dotnet.Shell.UI.Standard;
using dotshell.common;
using FirstRunWizard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace Dotnet.Shell
{
    internal sealed class Program : Disposable
    {
        private readonly IConsole _consoleInterface;
        private readonly ErrorDisplay _errorDisplay;
        private readonly ShellExecutor _executor;

        internal Program()
        {
            _consoleInterface = new DotNetConsole();
            _errorDisplay = new(_consoleInterface);
            _executor = ShellExecutor.GetDefaultExecuterAsync(_errorDisplay).GetAwaiter().GetResult();
        }

        public async Task RunAsync(string[] args)
        {
            ConfigureProfile();
            ProfileOptimization.StartProfile("Startup.Profile");

            Parser.Default.ParseArguments<Settings>(args).WithParsed<Settings>(o => Settings.Default = o);

            Settings.Default.AddComplexDefaults();
            await WaitForDebuggerAttach();

            if (!Settings.Default.DontRunWizard && !FirstRunWizard.WizardUi.Run())
            {
                ConsoleEx.WriteLine("First Time Wizard must be run", Color.Red);
                return;
            }

            if (Settings.Default.HistoryMode)
            {
                ProfileOptimization.StartProfile("History.Profile");
                try
                {
                    var historyTask = OS.GetOSHistoryAsync();
                    var box = new HistoryBox(_consoleInterface);
                    var result = await box.RunInterfaceAsync((await historyTask).ToList());

                    var response = API.HistoryApi.SearchResultAsync(result, Settings.Default.APIPort, Settings.Default.Token);

                    Console.Clear();

                    await response;
                }
                catch (Exception ex)
                {
                    _errorDisplay.PrettyException(ex);
                    await Task.Delay(5 * 1000);
                }
            }
            else
            {
                if (!await ExcutedCommandLineScript(args))
                {
                    ProfileOptimization.StartProfile("Interactive.Profile");
                    await StartInteractiveModeAsync(Settings.Default.UX);
                }
            }
        }

        private async Task<bool> ExcutedCommandLineScript(string[] args)
        {
            var fileArguments = args.Where(x => x.EndsWith(".csx") || x.EndsWith(".cs") || x.EndsWith(Dotnet.Shell.API.Shell.DefaultScriptExtension)).ToArray();

            if (fileArguments.Length > 0)
            {
                ProfileOptimization.StartProfile("Script.Profile");

                var script = fileArguments[0];

                var scriptArgs = new List<string>();

                for (int x = 0; x < args.Length; x++)
                {
                    if (args[x] == script)
                    {
                        var scriptOptions= args.Skip(x).ToArray();
                        scriptArgs.AddRange(scriptOptions);
                        break;
                    }
                }

                _executor.Args.AddRange(scriptArgs);

                await _executor.ExecuteFileAsync(script);

                return true;
            }

            return false;
        }

        private static async Task WaitForDebuggerAttach()
        {
            if (Settings.Default.EarlyDebuggerAttach)
            {
                ConsoleEx.WriteLine($"Now connect your debugger to {Environment.ProcessId}", Color.GhostWhite );
                while (!Debugger.IsAttached)
                {
                    Console.Write(".");
                    await Task.Delay(1000).ConfigureAwait(false);
                }
                Console.Clear();
            }
        }

        private static void ConfigureProfile()
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nsh");
            if (Directory.Exists(configDir))
            {
                ProfileOptimization.SetProfileRoot(Path.Combine(configDir, "profiles"));
            }
        }

        public static async Task Main(string[] args)
        {
            using var p = new Program();
            await p.RunAsync(args).ConfigureAwait(false);
        }

        private async Task StartInteractiveModeAsync(UserExperience ux)
        {
            using (var interactiveModeCancellationSource = new CancellationTokenSource())
            {
                //var historyWritingTask = Task.Run(async () =>
                //{

                //    while (!interactiveModeCancellationSource.IsCancellationRequested)
                //    {
                //        await Task.Delay(TimeSpan.FromSeconds(60), interactiveModeCancellationSource.Token);

                //        if (!interactiveModeCancellationSource.IsCancellationRequested)
                //        {
                //            var localCopy = historyToWrite;

                //            Interlocked.Exchange(ref historyToWrite, new List<HistoryItem>());

                //            if (localCopy.Count != 0)
                //            {
                //                await OS.WriteHistoryAsync(localCopy);
                //            }
                //        }
                //    }
                //}, interactiveModeCancellationSource.Token);

                CancellationTokenSource ctrlCCancellationSource = null;

                Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs args) =>
                {
                    args.Cancel = true;
                    _executor?.Shell?.ForegroundProcess?.SignalTerminate();
                    ctrlCCancellationSource?.Cancel();
                };

                await LoadAndExecuteCoreScriptAsync().ConfigureAwait(false);

                var improvedConsole = new ConsoleImproved(_consoleInterface, _executor.Shell);

                var searchFunction = ux == UserExperience.Classic ? HistorySearch.OnSearchHistory(_consoleInterface, _executor.Shell) :
                                     ux == UserExperience.Enhanced ? HistoryBox.OnSearchHistory(_consoleInterface) : HistoryBox.OnSearchHistoryTmux();

                var suggestor = new Logic.Suggestions.Suggestions(_executor.Shell);

                improvedConsole.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.R, ConsoleModifiers.Control), searchFunction);
                improvedConsole.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Tab), suggestor.OnTabSuggestCmdAsync);

                await ConsoleLoopAsync(improvedConsole, ctrlCCancellationSource, interactiveModeCancellationSource).ConfigureAwait(false);
            }
        }

        private async Task ConsoleLoopAsync(ConsoleImproved improvedConsole, CancellationTokenSource ctrlCCancellationSource, CancellationTokenSource interactiveModeCancellationSource)
        {
            var historyToWrite = CreateWriteHistoryCache();

            while (true) // main command loop
            {
                improvedConsole.DisplayPrompt();
                string input;

                //await _executor.Shell.HistoryLoadedTask;

                try
                {
                    using (ctrlCCancellationSource = new CancellationTokenSource())
                    {
                        input = await improvedConsole.GetCommandAsync(ctrlCCancellationSource.Token);
                    }
                    ctrlCCancellationSource = null;
                }
                catch (TaskCanceledException ex)
                {
                    if (!ctrlCCancellationSource.IsCancellationRequested)
                    {
                        _errorDisplay.PrettyException(ex);
                    }
                    continue;
                }
                catch (Exception ex)
                {
                    _errorDisplay.PrettyException(ex);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(input))
                {
                    if (input == "#reload_scripts")
                    {
                        ConsoleEx.WriteLine("Re-downloading util scripts", Color.OrangeRed);
                        WizardUi.DownloadCoreScripts();
                        continue;
                    }

                    try
                    {
                        input = _executor.Shell.TryReplaceWithCSAlias(input);
                        await _executor.ExecuteAsync(input);
                    }
                    catch (ExitException)
                    {
                        await interactiveModeCancellationSource.CancelAsync();
                        await OS.WriteHistoryAsync(historyToWrite);
                        break;
                    }
                    catch (PreProcessorSyntaxException ex)
                    {
                        _errorDisplay.PrettyException(ex);
                    }
                    catch (Exception ex)
                    {
                        _errorDisplay.PrettyException(ex, input);
                    }
                }
            }
        }

        private List<HistoryItem> CreateWriteHistoryCache()
        {
            var historyToWrite = new List<HistoryItem>();

            _executor.Shell.CommandHandlers.Add((cmd) =>
            {
                historyToWrite.Add(new HistoryItem(cmd, DateTime.UtcNow));
                return cmd;
            });

            return historyToWrite;
        }

        private async Task LoadAndExecuteCoreScriptAsync()
        {
            (var coreFound, var coreScript) = FindAndLoadCore();
            if (coreFound)
            {
                try
                {
                    await _executor.ExecuteAsync(coreScript).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occured loading core.nsh");
                    _errorDisplay.PrettyException(ex, coreScript);
                }
            }
        }

        private static (bool success, string content) FindAndLoadCore()
        {
            if (File.Exists(Settings.Default.ProfileScriptPath))
            {
                try
                {
                    var content = File.ReadAllText(Settings.Default.ProfileScriptPath);
                    return (true, content);
                }
                catch
                {
                    // ignore
                }
            }

            return (false, null);
        }
    }
}
