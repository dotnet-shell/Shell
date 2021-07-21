# Customizing your prompt
Dotnet-shell supports prompt customization through the [Shell.Prompt API function](/api/Dotnet.Shell.API.Shell.html#Dotnet_Shell_API_Shell_Prompt). To use this simply declare a function that will be called everytime a prompt is to be rendered and set this to Shell.Prompt. You can do this at any time but doing this in core.nsh will mean your prompt will always be displayed first.

## Powerline

By default dotnet-shell ships with basinc support for Powerline - you can find this in core.nsh. A terse example is given here:

```
Shell.Prompt = () => 
{
    string powerLinePrompt=`powerline-render shell left --last-exit-code=$Shell.LastExitCode$`;
    return ColorString.FromRawANSI( powerLinePrompt );
};
```

## Starship 

[Starship](https://starship.rs/) support can be added to dotnet-shell using the following syntax in you core.nsh file.

```
Shell.Prompt = () =>
    var prompt = `starship prompt --status=$Shell.LastExitCode$ --jobs=$Shell.BackgroundProcesses.Count()$`;
    return ColorString.FromRawANSI(prompt);
};
```