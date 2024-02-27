// description: Provides Bash-like 'fg' command
// author: @therealshodan
using System.Drawing;
using Dotnet.Shell.UI;

void BashForegroundCmd(string args)
{
    if (string.IsNullOrWhiteSpace(args) && Shell.BackgroundProcesses.Any())
    {
        Shell.BackgroundProcesses.First().WaitTillExit(Shell);
    }
    else
    {
        if (!int.TryParse(args, out int pid))
        {
            Console.WriteLine(new ColorString("Error: invalid ID",Color.Red).TextWithFormattingCharacters);
        }
        else
        {
            var matchingProcesses = Shell.BackgroundProcesses.Where(p => p.Process.Id == pid);
            if (matchingProcesses.Any())
            {
                matchingProcesses.First().WaitTillExit(Shell);
            }
            else
            {
                Console.WriteLine(new ColorString("Error: no matching ID",Color.Red).TextWithFormattingCharacters);
            }
        }
    }
}

Shell.AddCSAlias("fg", "BashForegroundCmd(\"{0}\");");
