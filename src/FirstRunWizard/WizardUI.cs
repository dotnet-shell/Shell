using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FirstRunWizard
{
    public class WizardUI
    {
        private string firstRunFilename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nsh", ".firstrun");

        /// <summary>
        /// Runs the Wizard UI interactively.
        /// </summary>
        /// <returns>If False stop execution of the shell, user need to do work</returns>
        public static bool Run()
        {
            WizardUI wiz = new WizardUI();
            return wiz.StartInteractive();
        }

        public bool StartInteractive()
        {
            bool ret = false;

            if (HasWizardRunBefore())
            {
                ret = true;
                return ret;
            }

            bool createFirstRunFile = true;

            Console.WriteLine("This looks like the first run of dotnet-shell, do you want to run the initial set up wizard (highly recommended)?");
            if (GetYesOrNo())
            {
                Console.WriteLine();

                if (PreRequisitesCheck())
                {
                    Console.WriteLine("Do you want to auto-accept the recommended defaults (recommended)?");
                    var autoAccept = GetYesOrNo();
                    Console.WriteLine();

                    var dirName = Path.GetDirectoryName(firstRunFilename);
                    if (!Directory.Exists(dirName))
                    {
                        Directory.CreateDirectory(dirName);
                    }
                    Console.WriteLine();

                    Console.WriteLine("Create default core.nsh (autorun profile) in ~/.nsh/ (highly recommended)?");
                    if (autoAccept || GetYesOrNo())
                    {
                        CreateDefaultProfile();
                    }
                    Console.WriteLine();

                    Console.WriteLine("Create default aliases.nsh (Bash-like aliases) in ~/.nsh/ (recommended)?");
                    if (autoAccept || GetYesOrNo())
                    {
                        CreateDefaultAliases();
                    }
                    Console.WriteLine();

                    Console.WriteLine("Download useful scripts from GitHub (https://github.com/dotnet-shell/CoreScripts) and store in ~/.nsh/functions/ (highly recommended)?");
                    if (autoAccept || GetYesOrNo())
                    {
                        DownloadCoreScripts();
                    }
                    else
                    {
                        CreateEmptyCoreScriptsDir();
                    }
                    Console.WriteLine();

                    if (IsWindowsTerminal())
                    {
                        ShowWindowsTerminalConfig();
                    }
                    Console.WriteLine();

                    Console.WriteLine("More configuration guides for Tmux, Vim and Windows Terminal can be found on online");
                    ret = true;
                }
                else
                {
                    createFirstRunFile = false;
                    ret = false;
                }
            }

            if (createFirstRunFile)
            {
                Console.WriteLine("To run this wizard again remove " + firstRunFilename);
                CreateWizardAutorunFile();
            }

            return ret;
        }

        private bool HasWizardRunBefore()
        {           
            return File.Exists(firstRunFilename);
        }

        private void CreateWizardAutorunFile()
        {
            using (var fs = File.Create(firstRunFilename))
            {
                fs.Close();
            }
        }

        private void ShowWindowsTerminalConfig()
        {
            const string Data = @"If you use Windows Terminal you can easily access dotnet-shell by including a new environment in your
Settings file such as in the following snippet:
      {
        ""guid"": ""{YOUR DISTRO GUID}"",
        ""hidden"": false,
        ""name"": ""OS NAME (dotnet-shell)"",
        ""bellStyle"": ""none"",
        ""commandline"": ""bash -c \""/usr/local/bin/tmux -2 new-session '~/.dotnet/tools/dotnet-shell'\""""
      }";

            Console.WriteLine(Data);
            Console.WriteLine("If you want to use tmux enhanced mode, use the following command line in your settings.json");
            Console.WriteLine(@"""commandline"": ""bash -c \""/usr/local/bin/tmux -2 new-session '~/.dotnet/tools/dotnet-shell --ux TmuxEnhanced'\""""");
        }

        private void DownloadCoreScripts()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nsh", "functions");
            RunAndGetStdOut("git", "clone https://github.com/dotnet-shell/CoreScripts \""+dir+"\"");
        }

        private void CreateEmptyCoreScriptsDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nsh", "functions");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private void CreateDefaultAliases()
        {
            var core = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nsh", "aliases.nsh");
            var defaultAliases = GetEmbeddedResource("defaults/aliases", Assembly.GetExecutingAssembly());

            File.WriteAllText(core, defaultAliases);
        }

        private void CreateDefaultProfile()
        {
            var core = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nsh", "core.nsh");
            var defaultProfile = GetEmbeddedResource("defaults/core", Assembly.GetExecutingAssembly());

            File.WriteAllText(core, defaultProfile);
        }

        private bool GetYesOrNo()
        {
            while (true)
            {
                Console.Write("Y|N) ");
                var key = Console.ReadLine().Trim().ToUpperInvariant();
                if (key == "Y" || key == "N")
                {
                    return key == "Y";
                }
            }
        }

        public bool PreRequisitesCheck()
        {
            if (string.IsNullOrWhiteSpace(RunAndGetStdOut("git", "--version")))
            {
                Console.WriteLine("Git could not be found!");
                Console.WriteLine("You can continue without Git but you won't be able to use the highly recommended useful scripts");
                Console.WriteLine("Do you want to stop and install Git?");
                if (!GetYesOrNo())
                {
                    return false;
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (!IsSupportedTmuxVersionInstalled())
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsSupportedTmuxVersionInstalled()
        {
            if (!DoesTmuxSupportPopups())
            {
                Console.WriteLine("The version of tmux installed is too old, or not available.");
                Console.WriteLine("In order to use the tmux enhanced version of dotnet-shell a version of tmux >= 3.2 must be installed in your path");
                Console.WriteLine("Please see https://github.com/tmux/tmux");

                Console.WriteLine("Do you want to stop and install tmux? (Most people don't have the latest version of tmux so its OK to say N here)");
                if (!GetYesOrNo())
                {
                    return false;
                }
            }

            return true;
        }

        private bool DoesTmuxSupportPopups()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var version = RunAndGetStdOut("tmux", "-V");
                return version != null && version.StartsWith("tmux 3.2");
            }
            return false;
        }

        public bool IsWSL()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (var variable in Environment.GetEnvironmentVariables().Keys)
                {
                    var name = variable.ToString();

                    if (name == "WSL_DISTRO_NAME" || name == "WSL_INTEROP" || name == "WSLENV")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool IsWindowsTerminal()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (var variable in Environment.GetEnvironmentVariables().Keys)
                {
                    var name = variable.ToString();

                    if (name == "WT_PROFILE_ID" || name == "WT_SESSION")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private string RunAndGetStdOut(string program, string arguments)
        {
            var proc = new Process();
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.FileName = program;
            proc.StartInfo.Arguments = arguments;
            proc.StartInfo.UseShellExecute = false;

            proc.Start();
            proc.WaitForExit();

            return proc.StandardOutput.ReadToEnd();
        }
        public static string GetEmbeddedResource(string resourceName, Assembly assembly)
        {
            resourceName = assembly.GetName().Name + "." + resourceName.Replace(" ", "_")
                                                       .Replace("\\", ".")
                                                       .Replace("/", ".");

            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                    return null;

                using (StreamReader reader = new StreamReader(resourceStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
