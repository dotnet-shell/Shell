// description: Autocompletes commands straight from history
// author: @therealshodan
using System.Drawing;
using Dotnet.Shell.Logic.Suggestions;
using Dotnet.Shell.UI;

async Task<IEnumerable<Suggestion>> HistoryCompletion(string userText, int cursorPos)
{
    // ignore short strings as walking history can be expensive
    // change this if you'd like more autocompletes
    if (cursorPos < 10)
    {
        return null;
    }

    var sanitizedText = userText.Substring(0, cursorPos).Trim();

    return Shell.History
        .Select(x => x.CmdLine.Trim())
        .Distinct()
        .Where(x => x.StartsWith(sanitizedText))
        .Select(x => new Suggestion() { 
            Index = cursorPos,
            CompletionText = x.Remove(0, sanitizedText.Length),
            FullText = new ColorString(x, Color.Yellow)
        });
}

Shell.AutoCompletionHandlers.Add(HistoryCompletion);
