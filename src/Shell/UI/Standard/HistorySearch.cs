using Dotnet.Shell.Logic.Console;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.Shell.UI.Standard
{
    internal struct SearchHistory
    {
        public string Term;
        public List<string> SearchResults;
        public int SelectedItem;
    }

    internal class HistorySearch
    {
        private IConsole implementation;
        private Dotnet.Shell.API.Shell shell;
        private ConsoleImproved ci = null;

        public static Func<ConsoleImproved, ConsoleKeyEx, Task> OnSearchHistory(IConsole console, Dotnet.Shell.API.Shell shell)
        {
            var search = new HistorySearch(console, shell);
            return search.OnSearchHistoryAsync;
        }

        public HistorySearch(IConsole console, Dotnet.Shell.API.Shell shell)
        {
            this.implementation = console;
            this.shell = shell;
        }

        private async Task OnSearchHistoryAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            int oldPos = implementation.CursorTop;

            implementation.CursorLeft = 0;
            implementation.CursorTop = implementation.WindowHeight - 2;

            // create fake shell object with a custom prompt
            var fakeShell = new Dotnet.Shell.API.Shell();
            fakeShell.History.AddRange(shell.History);
            fakeShell.Prompt = () =>
            {
                RenderSearchChanges();

                // Search) user search text
                // [1/3]: matched entry
                return "Search) ";
            };

            ci = new ConsoleImproved(implementation, fakeShell);

            ci.KeyOverrides.Where(x => x.Key.Key == ConsoleKey.UpArrow).ToList().ForEach(x => ci.KeyOverrides.Remove(x));
            ci.KeyOverrides.Where(x => x.Key.Key == ConsoleKey.DownArrow).ToList().ForEach(x => ci.KeyOverrides.Remove(x));

            ci.AddKeyOverride(ConsoleKeyEx.Any, OnSearchTextEnteredAsync);
            ci.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.UpArrow), OnChangeSearchEntryAsync);
            ci.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.DownArrow), OnChangeSearchEntryAsync);
            ci.AddKeyOverride(new ConsoleKeyEx(ConsoleKey.Enter), OnSelectSearchEntryAsync);

            ci.DisplayPrompt();

            // When the prompt returns, instead of executing the command we just set that
            // as what to show on screen
            var command = await ci.GetCommandAsync();

            implementation.CursorTop = implementation.WindowHeight - 2;
            implementation.Write(new string(' ', implementation.WindowWidth));
            implementation.CursorTop = implementation.WindowHeight - 1;
            implementation.Write(new string(' ', implementation.WindowWidth));
            
            implementation.CursorTop = oldPos;
            implementation.Write(new string(' ', implementation.WindowWidth));

            prompt.DisplayPrompt(command);
        }

        private void RenderSearchChanges(SearchHistory? searchHistory = null)
        {
            var totalItems = !searchHistory.HasValue ? shell.History.Count : searchHistory.Value.SearchResults.Count;
            var currentItem = !searchHistory.HasValue ? 0 : searchHistory.Value.SelectedItem + 1;
            var match = !searchHistory.HasValue || searchHistory.Value.SearchResults.Count == 0 ? string.Empty : searchHistory.Value.SearchResults[searchHistory.Value.SelectedItem];

            var minimalPrompt = "[" + currentItem + "/" + totalItems + "]: ";

            var matchedEntryMaxLength = implementation.WindowWidth - minimalPrompt.Length;
            if (match.Length > matchedEntryMaxLength)
            {
                match = match.Substring(0, matchedEntryMaxLength);
            }

            ColorString highlightedLine = string.Empty;

            if (searchHistory.HasValue && !string.IsNullOrWhiteSpace(match))
            {
                var notMatchedString = new StringBuilder();
                var matchingPositions = FindAllIndexesOf(match, searchHistory.Value.Term);
                for (int posInStr = 0; searchHistory != null && posInStr < match.Length; posInStr++)
                {
                    if (matchingPositions.Contains(posInStr))
                    {
                        highlightedLine += notMatchedString.ToString() + new ColorString(searchHistory.Value.Term, Color.Green);
                        notMatchedString.Clear();
                        posInStr += searchHistory.Value.Term.Length - 1;
                    }
                    else
                    {
                        notMatchedString.Append(match[posInStr]);
                    }
                }

                highlightedLine += notMatchedString.ToString();
            }

            // need to use the implementation functions as we are writing off the current line
            var pos = implementation.CursorLeft;

            implementation.CursorVisible = false;
            implementation.WriteLine();
            implementation.Write(new string(' ', implementation.WindowWidth - 1));
            implementation.CursorLeft = 0;

            implementation.Write(minimalPrompt);

            // our write function sets both implementation.CursorLeft and CursorPosition
            // we need to restore these to ensure we dont go out of sync
            var cpOld = ci.CursorPosition;
            ci.CursorPosition = minimalPrompt.Length;
            ci.Write(highlightedLine);
            ci.CursorPosition = cpOld;

            implementation.CursorTop--;
            implementation.CursorLeft = pos;
            implementation.CursorVisible = true;
        }

        private int[] FindAllIndexesOf(string line, string term)
        {
            if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(term))
            {
                return new int[0];
            }

            var matches = new List<int>();

            var index = 0;
            while (index != -1)
            {
                index = line.IndexOf(term, index);
                if (index != -1)
                {
                    matches.Add(index);
                    index = index + term.Length;
                }
                else
                {
                    break;
                }
            }
            return matches.ToArray();
        }

        private Task OnSearchTextEnteredAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (key.Key.Value == ConsoleKey.UpArrow || key.Key.Value == ConsoleKey.DownArrow)
                {
                    return;
                }

                var text = ci.UserEnteredText.ToString();

                var primaryResults = shell.History.Where(x => x.CmdLine.Contains(text));
                var secondaryResults = shell.History.Where(x => x.CmdLine.Contains(text.Trim()));

                var search = new SearchHistory()
                {
                    SearchResults = primaryResults.Union(secondaryResults).Select(x => x.CmdLine).Distinct().ToList(),
                    SelectedItem = 0,
                    Term = text
                };
                RenderSearchChanges(search);
                ci.Tag = search;
            });
        }

        private Task OnChangeSearchEntryAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (ci.Tag is SearchHistory)
                {
                    var results = (SearchHistory)ci.Tag;

                    if (key.Key == ConsoleKey.DownArrow)
                    {
                        if (results.SelectedItem < results.SearchResults.Count - 1)
                        {
                            results.SelectedItem++;
                        }
                    }
                    else
                    {
                        if (results.SelectedItem > 0)
                        {
                            results.SelectedItem--;
                        }
                    }

                    RenderSearchChanges(results);
                    ci.Tag = results;
                }
            });
        }

        private Task OnSelectSearchEntryAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (ci.Tag is SearchHistory)
                {
                    var search = (SearchHistory)ci.Tag;
                    if (search.SearchResults.Any())
                    {
                        ci.UserEnteredText.Clear();
                        ci.UserEnteredText.Append(search.SearchResults[search.SelectedItem]);
                    }
                }
            });
        }
    }
}
