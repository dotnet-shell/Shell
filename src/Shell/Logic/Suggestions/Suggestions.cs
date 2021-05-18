using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System;
using System.Runtime.CompilerServices;
using Dotnet.Shell.Logic.Console;
using Dotnet.Shell.UI;
using System.IO;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("UnitTests")]

namespace Dotnet.Shell.Logic.Suggestions
{
    /// <summary>
    /// Class which refines an auto suggestion result
    /// </summary>
    public class Suggestion
    {
        /// <summary>
        /// Gets or sets the full text. This might be something like WriteLine when Console.Wri was typed
        /// </summary>
        /// <value>
        /// The full text.
        /// </value>
        public ColorString FullText { get; set; }

        /// <summary>
        /// Gets or sets the completion text. This is the substring to be added to the current command line input text.
        /// This might be something like teLine when Console.Wri was typed
        /// </summary>
        /// <value>
        /// The completion text.
        /// </value>
        public string CompletionText { get; set; }

        /// <summary>
        /// Gets or sets the index. This is the position the new text should be inserted at
        /// </summary>
        /// <value>
        /// The index.
        /// </value>
        public int Index { get; set; }
    }

    class Suggestions
    {
        private readonly CmdSuggestions cmdSuggestionsEngine;
        private readonly CSharpSuggestions cSharpSuggestionsEngine;
        private readonly Task<string[]> commandsInPath;

        private string textWhichGeneratedSuggestions = string.Empty;
        private int textPosWhichGeneratedSuggestions = -1;
        private List<Suggestion> currentSuggestionsList = new List<Suggestion>();
        private int currentlySelectedSuggestion = -1;
        private bool hasTabJustBeenPressedForGroupOfSuggestions = false;

        private bool NeedsNewSuggestions(ConsoleImproved prompt) =>
            textWhichGeneratedSuggestions != prompt.UserEnteredText.ToString() ||
            textPosWhichGeneratedSuggestions != prompt.UserEnteredTextPosition ||
            currentlySelectedSuggestion == -1 ||
            currentSuggestionsList.Count == 0;

        public Suggestions(API.Shell shell)
        {
            commandsInPath = Task.Run(() =>
            {
                var ret = new List<string>();

                ret.AddRange(shell.cmdAliases.Keys);
                ret.AddRange(shell.csAliases.Keys);

                foreach (var path in shell.Paths)
                {
                    ret.AddRange(Directory.GetFiles(path).Select(x => x.Remove(0, path.Length + 1)));
                    // todo check executable bit
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    const string EXE = ".exe";
                    return ret.Where(x => x.EndsWith(EXE)).Select(x => x.Substring(0, x.Length - EXE.Length)).OrderBy(x => x.Length).ToArray();
                }
                else
                {
                    return ret.OrderBy(x => x.Length).ToArray();
                }
            });

            cmdSuggestionsEngine = new CmdSuggestions(shell, commandsInPath);
            cSharpSuggestionsEngine = new CSharpSuggestions(commandsInPath);
        }

        public async Task OnTabSuggestCmdAsync(ConsoleImproved prompt, ConsoleKeyEx key)
        {
            await performCompletionAsync(prompt, key);
        }

        private async Task performCompletionAsync(ConsoleImproved prompt, ConsoleKeyEx key, int depth = 0)
        {
            if (NeedsNewSuggestions(prompt))
            {
                textWhichGeneratedSuggestions = prompt.UserEnteredText.ToString();
                textPosWhichGeneratedSuggestions = prompt.UserEnteredTextPosition;

                // invalidate state
                hasTabJustBeenPressedForGroupOfSuggestions = false;
                currentlySelectedSuggestion = -1;

                // try to get some suggestions, in the future i'd like this to be an API people can register to they can
                // write their own completion routines
                List<Task<IEnumerable<Suggestion>>> suggestions2 = new List<Task<IEnumerable<Suggestion>>>();
                suggestions2.Add(cmdSuggestionsEngine.GetSuggestionsAsync(textWhichGeneratedSuggestions, prompt.UserEnteredTextPosition));
                suggestions2.Add(cSharpSuggestionsEngine.GetSuggestionsAsync(textWhichGeneratedSuggestions, prompt.UserEnteredTextPosition));
                suggestions2.AddRange(prompt.Shell.AutoCompletionHandlers.Select(x => x(textWhichGeneratedSuggestions, prompt.UserEnteredTextPosition)));

                await Task.WhenAll(suggestions2);

                var newSuggestions = suggestions2.Select(x => x.Result).Where(x => x != null).SelectMany(x => x).ToList();

                if (newSuggestions.Count != 0)
                {
                    currentSuggestionsList = newSuggestions;
                    currentlySelectedSuggestion = 0;

                    // We might have a common prefix to all our results, in which case we can fill that in for
                    // the user so they have less to type. 
                    var autoCompleteResults = currentSuggestionsList.Select(x => x.CompletionText);

                    var commonPrefix = new string(autoCompleteResults.First().Substring(0, autoCompleteResults.Min(s => s.Length))
                        .TakeWhile((c, i) => autoCompleteResults.All(s => s[i] == c)).ToArray());

                    // If we have autocompleted something we need to call this function again to update our state
                    // like if the user had typed the text in themselves
                    // But we only want to do this once as otherwise we could just keep on auto completing.
                    if (!string.IsNullOrWhiteSpace(commonPrefix) && depth == 0)
                    {
                        var commonIndex = currentSuggestionsList.First(x => x.CompletionText.StartsWith(commonPrefix)).Index;
                        prompt.ReplaceUserEntryAtPosition(commonPrefix, commonIndex);

                        // because we've changed the onscreen text we need to requery to get a better list of suggestions
                        await performCompletionAsync(prompt, key, depth + 1);
                        return;
                    }
                }
                else
                {
                    prompt.IgnoreTab();
                }
            }
            else
            {
                // increment the current index we are rendering
                currentlySelectedSuggestion = (currentlySelectedSuggestion + 1) % currentSuggestionsList.Count;
            }

            // now to render what we have, but first check if we've got a list in which case a double tab combo will render it
            // ignore all this if we our just refreshing our state
            if (depth == 0 && currentSuggestionsList.Count > 1)
            {
                if (hasTabJustBeenPressedForGroupOfSuggestions)
                {
                    await RenderAllSuggestionsAsync(prompt);
                }
                else
                {
                    prompt.IgnoreTab();
                    hasTabJustBeenPressedForGroupOfSuggestions = true;
                }
            }
            else if (depth == 0 && currentlySelectedSuggestion != -1)
            {
                RenderSuggestion(prompt, currentlySelectedSuggestion);
            }
        }

        private void RenderSuggestion(ConsoleImproved prompt, int suggestionIndex)
        {
            var text = currentSuggestionsList[suggestionIndex].CompletionText;
            var position = currentSuggestionsList[suggestionIndex].Index;

            prompt.ReplaceUserEntryAtPosition(text, position);
        }

        private async Task RenderAllSuggestionsAsync(ConsoleImproved prompt)
        {
            var cmd = prompt.UserEnteredText.ToString();
            var pos = prompt.CursorPosition;

            if (currentSuggestionsList.Count >= 100)
            {
                prompt.WriteLine();
                prompt.Write("Display all " + currentSuggestionsList.Count + " possibilities? (y or n) ");
                if (await prompt.GetCharAsync() == "Y")
                {
                    PerformRenderAll(prompt);
                }
            }
            else
            {
                PerformRenderAll(prompt);
            }

            prompt.DisplayPrompt();
            prompt.ReplaceUserEntryAtPosition(cmd, 0);
            prompt.CursorPosition = pos;
        }

        private void PerformRenderAll(ConsoleImproved prompt)
        {
            const int MinSpacing = 2;
            var width = prompt.Width;

            var longestStringLength = currentSuggestionsList.Max(x => x.FullText.Length + MinSpacing);
            var numberPerLine = (int)Math.Floor((double)width / (longestStringLength));

            var b = new StringBuilder();
            int writtenEntries = 0;

            prompt.WriteLine(b.ToString());

            foreach (var suggestion in currentSuggestionsList)
            {
                if (writtenEntries >= numberPerLine)
                {
                    writtenEntries = 0;
                    prompt.WriteLine(b.ToString());
                    b.Clear();
                }

                b.Append(suggestion.FullText.TextWithFormattingCharacters + new string(' ', longestStringLength - suggestion.FullText.Length));
                writtenEntries++;
            }

            if (b.Length != 0)
            {
                prompt.WriteLine(b.ToString());
            }
            else
            {
                prompt.WriteLine();
            }
        }
    }
}
