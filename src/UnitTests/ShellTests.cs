using Dotnet.Shell.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Reflection;

namespace UnitTests
{
    [TestClass]
    public class ShellTests
    {
        [TestMethod]
        public void Construct()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
            }
        }

        [TestMethod]
        public void AddAliases()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.AddCSAlias("echo", "Console.WriteLine(\"{0}\");");
                fakeShell.AddCSAlias("red", "Console.WriteLine(new ColorString(\"{0}\", Color.Red).TextWithFormattingCharacters);");
                fakeShell.AddCSAlias("green", "Console.WriteLine(new ColorString(\"{0}\", Color.Green).TextWithFormattingCharacters);");
                fakeShell.AddCSAlias("quit", "Environment.Exit(0);");

                fakeShell.AddCmdAlias("ls", "ls --color=auto ");
                fakeShell.AddCmdAlias("dir", "dir --color=always ");
                fakeShell.AddCmdAlias("vdir", "vdir --color=always ");
                fakeShell.AddCmdAlias("grep", "grep --color=always ");
                fakeShell.AddCmdAlias("fgrep", "fgrep --color=alway s");
                fakeShell.AddCmdAlias("egrep", "egrep --color=always ");
                fakeShell.AddCmdAlias("ll", "ls -alF ");
                fakeShell.AddCmdAlias("la", "ls -A ");
                fakeShell.AddCmdAlias("l", "ls -CF ");
            }
        }

        [TestMethod]
        public void DuplicateAlias()
        {
            int errorCount = 0;

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.Error = (msg) => { Assert.IsTrue(!string.IsNullOrWhiteSpace(msg)); errorCount++; };
                fakeShell.AddCSAlias("echo", "Console.WriteLine(\"{0}\");");
                fakeShell.AddCSAlias("echo", "sdfsdfsdf");

                fakeShell.AddCmdAlias("l", "ls -CF ");
                fakeShell.AddCmdAlias("l", "ls -CF ");                
            }

            Assert.AreEqual(2, errorCount);
        }

        [TestMethod]
        public void ChangeDir()
        {
            var testDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.ChangeDir(Path.GetFullPath(testDir + @"\TestFiles"));
                Assert.IsTrue(fakeShell.WorkingDirectory.EndsWith("TestFiles"));

                fakeShell.ChangeDir("..");
                Assert.IsTrue(fakeShell.WorkingDirectory == testDir);
            }
        }
    }
}
