// description: Provides asyncronous user alerting for other scripts
// author: @therealshodan
using System.Drawing;
using System.Collections.Concurrent;
using Dotnet.Shell.UI;

var statusMessages = new ConcurrentQueue<ColorString>();

bool TryShowStatusMessage()
{
    if (statusMessages.Count > 0)
    {
        return false;
    }

    if (Shell.ForegroundProcess != null)
    {
        return false;
    }

    bool ret = false;
    while (statusMessages.TryDequeue(out ColorString msg))
    {
        ret = true;
        Console.WriteLine(msg.TextWithFormattingCharacters);
    }
    return ret;
}

void StatusMessage(ColorString msg, string from=null)
{
    var title = string.IsNullOrWhiteSpace(from) ?
        new ColorString(string.Format("Message @ {1} ", DateTime.Now), Color.Yellow) :
        new ColorString(string.Format("Message from {0} @ {1} ", from, DateTime.Now), Color.Yellow);
    var header = new ColorString("***", Color.Yellow);
    var spacer = new ColorString(" - ", Color.Yellow);
    statusMessages.Enqueue(title + header + spacer + msg + spacer + header);
}

void StatusMessage(string msg, string from=null)
{
    StatusMessage(new ColorString(msg, Color.Yellow), from);
}

// Replace the prompt with our prompt so we can render any messages after ENTER
// is pressed
var previousPrompt = Shell.Prompt;
Shell.Prompt = () =>
{
    TryShowStatusMessage();
    return previousPrompt();
}
