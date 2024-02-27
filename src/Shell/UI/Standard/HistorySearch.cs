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

    public class HistorySearch
    {
        private readonly IConsole implementation;
        private readonly Dotnet.Shell.API.Shell shell;
        private ConsoleImproved ci = null;

        public static Func<ConsoleImproved, ConsoleKeyEx, Task<bool>> OnSearchHistory(IConsole console, Dotnet.Shell.API.Shell shell)
        {
            var search = new HistorySearch(console, shell);
            return search.OnSearchHistoryAsync;
        }

        public HistorySearch(IConsole console, Dotnet.Shell.API.Shell shell)
        {
            implementation = console;
            this.shell = shell;
        }

        private async Task<bool> OnSearchHistoryAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            prompt.ClearUserEntry();

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
            ci.ClearUserEntry();

            // When the prompt returns, instead of executing the command we just set that
            // as what to show on screen
            var command = await ci.GetCommandAsync();

            implementation.CursorTop = implementation.WindowHeight - 2;
            implementation.Write(new string(' ', implementation.WindowWidth));
            implementation.CursorTop = implementation.WindowHeight - 1;
            implementation.Write(new string(' ', implementation.WindowWidth));
            
            implementation.CursorTop = oldPos;
            implementation.Write(new string(' ', implementation.WindowWidth));

            prompt.ClearUserEntry();
            prompt.DisplayPrompt(command, false);

            return false;
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
                var matchingPositions = FindAllIndexesOf(match.ToLowerInvariant(), searchHistory.Value.Term.ToLowerInvariant());
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

        private static int[] FindAllIndexesOf(string line, string term)
        {
            if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(term))
            {
                return Array.Empty<int>();
            }

            var matches = new List<int>();

            var index = 0;
            while (index != -1)
            {
                index = line.IndexOf(term, index);
                if (index != -1)
                {
                    matches.Add(index);
                    index += term.Length;
                }
                else
                {
                    break;
                }
            }
            return matches.ToArray();
        }

        private Task<bool> OnSearchTextEnteredAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (key.Key.Value == ConsoleKey.UpArrow || key.Key.Value == ConsoleKey.DownArrow)
                {
                    return false;
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

                return false;
            });
        }

        private Task<bool> OnChangeSearchEntryAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (ci.Tag is SearchHistory results)
                {
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

                return false;
            });
        }

        private Task<bool> OnSelectSearchEntryAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            return Task.Run(() =>
            {
                if (ci.Tag is SearchHistory search)
                {
                    if (search.SearchResults.Any())
                    {
                        ci.UserEnteredText = search.SearchResults[search.SelectedItem];
                    }
                }

                return false;
            });
        }
    }
}
