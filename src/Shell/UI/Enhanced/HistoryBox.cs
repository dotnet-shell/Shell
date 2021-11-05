using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConsoleGUI;
using ConsoleGUI.Common;
using ConsoleGUI.Controls;
using ConsoleGUI.Data;
using ConsoleGUI.Input;
using Dotnet.Shell.API;
using Dotnet.Shell.Logic;
using Dotnet.Shell.Logic.Console;
using Dotnet.Shell.Logic.Execution;

namespace Dotnet.Shell.UI.Enhanced
{
    internal class HistoryBox : IInputListener
    {
        private bool quit = false;
        private bool updateSearch = false;
        private readonly IConsole console = null;
        private TextBox searchBox = null;
        private ListView listView = null;

        public static Func<ConsoleImproved, ConsoleKeyEx, Task<bool>> OnSearchHistory(IConsole console)
        {
            var search = new HistoryBox(console);
            return search.OnSearchHistoryAltModeAsync;
        }

        public static Func<ConsoleImproved, ConsoleKeyEx, Task<bool>> OnSearchHistoryTmux()
        {
            return OnSearchHistoryTmuxModeAsync;
        }

        private async Task<bool> OnSearchHistoryAltModeAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            await console.SaveAsync();

            var command = await this.RunInterfaceAsync(prompt.Shell.History);

            await console.RestoreAsync();

            prompt.DisplayPrompt(command, false);

            return false;
        }

        private static async Task<bool> OnSearchHistoryTmuxModeAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            ProcessEx tmuxPopup = null;

            var result = HistoryAPI.ListenForSearchResultAsync((port, token) => {
                var cssCommand = string.Format(Settings.Default.HistoryPopupCommand, API.Shell.AssemblyLocation, port, token);
                var tmuxCommand = string.Format(Settings.Default.PopupCommand, cssCommand);

                // start tmux prompt
                tmuxPopup = OS.Exec(tmuxCommand);
            });

            await tmuxPopup.WaitForExitAsync();
            tmuxPopup.Dispose();

            await Task.WhenAny(result, Task.Delay(1000));

            if (result.IsCompletedSuccessfully)
            {
                prompt.DisplayPrompt(await result, false);
            }
            else
            {
                prompt.DisplayPrompt("Error", false);
            }

            return false;
        }

        public HistoryBox(IConsole console)
        {
            this.console = console;
        }

        private Control Build(List<string> history)
        {
            searchBox = new TextBox();
            listView = new ListView
            {
                Items = history
            };

            return new Background()
            {
                Color = new Color(0, 135, 175),
                Content = new DockPanel()
                {
                    FillingControl = listView,
                    DockedControl = new Boundary()
                    {
                        MaxHeight = 2,
                        Content = new Background()
                        {
                            Color = Color.Black,
                            Content = new VerticalStackPanel()
                            {
                                Children = new IControl[]
                                {
                                    new HorizontalSeparator(),
                                    new HorizontalStackPanel()
                                    {
                                        Children = new IControl[]
                                        {
                                            new TextBlock()
                                            {
                                                Text = "Search) ",
                                            },
                                            searchBox
                                        }
                                    }
                                }
                            }
                        }
                    },
                    Placement = DockPanel.DockedControlPlacement.Bottom
                }
            };
        }

        public async Task<string> RunInterfaceAsync(List<HistoryItem> history)
        {
            var historyToDisplay = history.Select(x => x.CmdLine).Distinct().ToList();
            var control = Build(historyToDisplay);

            ConsoleManager.Setup();
            ConsoleManager.Content = control;

            ConsoleManager.Resize(new ConsoleGUI.Space.Size(Console.WindowWidth, Console.WindowHeight));
            ConsoleManager.AdjustWindowSize();

            var inputListener = new IInputListener[]
            {
                this,
                listView,
                searchBox
            };
            quit = false;

            while (!quit)
            {
                ConsoleManager.AdjustBufferSize();
                ConsoleManager.ReadInput(inputListener);

                if (updateSearch)
                {
                    updateSearch = false;
                    var searchResults = historyToDisplay.Where(x => x.ToLowerInvariant().Contains(searchBox.Text.ToLowerInvariant())).ToList();
                    listView.Items = searchResults;
                }

                await Task.Delay(50);
            }

            return listView.SelectedItem;
        }

        public void OnInput(InputEvent inputEvent)
        {
            if (inputEvent.Key.Key == ConsoleKey.Enter)
            {
                quit = true;
            }
            else if (inputEvent.Key.Key != ConsoleKey.UpArrow && inputEvent.Key.Key != ConsoleKey.DownArrow)
            {
                updateSearch = true;
            }
        }
    }
}
