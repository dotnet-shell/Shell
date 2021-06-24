using Dotnet.Shell.API;
using Dotnet.Shell.Logic.Suggestions;
using Dotnet.Shell.Logic.Suggestions.Autocompletion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class CmdSuggestionsTests
    {
        private readonly string basePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestFiles");

        private static Task<string[]> FakeCommandsAsync()
        {
            return Task.FromResult(
                new string[] {
                    "atestcmd",
                    "btestcmd",
                    "testcmdc",
                    "testcmdd"
                }
            );
        }

        [TestMethod]
        public void Construct()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var s = new CmdSuggestions(fakeShell, FakeCommandsAsync());
            }
        }

        [TestMethod]
        public async Task SimpleQueryWithSingleResultAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.Paths.Add(basePath);

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("a", 1);

                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("testcmd", result.ElementAt(0).CompletionText);
                Assert.AreEqual(1, result.ElementAt(0).Index);

                result = await s.GetSuggestionsAsync("b", 1);

                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("testcmd", result.ElementAt(0).CompletionText);
                Assert.AreEqual(1, result.ElementAt(0).Index);
            }
        }

        [TestMethod]
        public async Task SimpleQueryWithoutResultAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.Paths.Add(basePath);

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("x", 1);

                Assert.AreEqual(0, result.Count());
            }
        }

        [TestMethod]
        public async Task QueryWithMultipleResultsAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.Paths.Add(basePath);

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var results = await s.GetSuggestionsAsync("testcm", 6);

                Assert.AreEqual(2, results.Count());

                foreach (var result in results)
                {
                    Assert.IsTrue(result.CompletionText == "dc" || result.CompletionText == "dd");
                    Assert.AreEqual(6, result.Index);
                }
            }
        }

        [TestMethod]
        public async Task WithinLargerStringAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.Paths.Add(basePath);

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("echo a; b; echo c; echo a", 9);

                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("testcmd", result.ElementAt(0).CompletionText);
                Assert.AreEqual(9, result.ElementAt(0).Index);
            }
        }

        [TestMethod]
        public async Task CompleteDirectoryAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.ChangeDir( Path.GetFullPath(basePath + "/../") );

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("cd T", 4);

                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("estFiles"+Path.DirectorySeparatorChar, result.ElementAt(0).CompletionText);
                Assert.AreEqual(4, result.ElementAt(0).Index);
            }
        }

        [TestMethod]
        public async Task CompleteDirectory_NotFoundAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.ChangeDir(Path.GetFullPath(basePath + "/../"));

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("cd X", 4);

                Assert.AreEqual(0, result.Count());
            }
        }

        [TestMethod]
        public async Task CompleteDirectory_WithinLargerCommandAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.ChangeDir(Path.GetFullPath(basePath + "/../"));

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("echo A; cd T", 12);
                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("estFiles"+ Path.DirectorySeparatorChar, result.ElementAt(0).CompletionText);
                Assert.AreEqual(12, result.ElementAt(0).Index);

                result = await s.GetSuggestionsAsync("echo A;cd T", 11);
                Assert.AreEqual(1, result.Count());
                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("estFiles"+ Path.DirectorySeparatorChar, result.ElementAt(0).CompletionText);
                Assert.AreEqual(11, result.ElementAt(0).Index);

                result = await s.GetSuggestionsAsync("echo A && cd T", 14);
                Assert.AreEqual(1, result.Count());
                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("estFiles"+ Path.DirectorySeparatorChar, result.ElementAt(0).CompletionText);
                Assert.AreEqual(14, result.ElementAt(0).Index);

                result = await s.GetSuggestionsAsync("cd T && cd A", 4);
                Assert.AreEqual(1, result.Count());
                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("estFiles"+ Path.DirectorySeparatorChar, result.ElementAt(0).CompletionText);
                Assert.AreEqual(4, result.ElementAt(0).Index);
            }
        }

        [TestMethod]
        public async Task CompleteFileAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.ChangeDir(Path.GetFullPath(basePath + "/../"));

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("cat TestFi", 10);

                Assert.AreEqual(1, result.Count());
            }
        }

        [TestMethod]
        public void InternalFileResolution_ConvertToAbsolute()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell
                {
                    HomeDirectory = basePath,
                    WorkingDirectory = basePath
                };

                Assert.AreEqual(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), FileAndDirectoryCompletion.ConvertToAbsolute(basePath + "/..", fakeShell));
                Assert.AreEqual(fakeShell.HomeDirectory + Path.DirectorySeparatorChar, FileAndDirectoryCompletion.ConvertToAbsolute("~/", fakeShell));
                Assert.AreEqual(fakeShell.WorkingDirectory + Path.DirectorySeparatorChar, FileAndDirectoryCompletion.ConvertToAbsolute("./", fakeShell));
                Assert.AreEqual(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "bob"), FileAndDirectoryCompletion.ConvertToAbsolute("~/../bob", fakeShell));
            }
        }

        [TestMethod]
        public async Task CompleteFilesInDirAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell
                {
                    HomeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                };

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("cat TestFiles/nsh", 17);

                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("ScriptTest.nsh", result.ElementAt(0).CompletionText);
                Assert.AreEqual(17, result.ElementAt(0).Index);
            }
        }

        [TestMethod]
        public async Task CompleteExecutableInDirAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell
                {
                    HomeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                };

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("."+Path.DirectorySeparatorChar+"TestFiles"+Path.DirectorySeparatorChar+"nshScr", 18);

                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("iptTest.nsh", result.ElementAt(0).CompletionText);
                Assert.AreEqual(18, result.ElementAt(0).Index);
            }
        }

        [TestMethod]
        public async Task CompleteExecutableInCurrentDirAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell
                {
                    HomeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    WorkingDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestFiles")
                };

                CmdSuggestions s = new(fakeShell, FakeCommandsAsync());
                var result = await s.GetSuggestionsAsync("." + Path.DirectorySeparatorChar + "nshScr", 8);

                Assert.AreEqual(1, result.Count());
                Assert.AreEqual("iptTest.nsh", result.ElementAt(0).CompletionText);
                Assert.AreEqual(8, result.ElementAt(0).Index);
            }
        }
    }
}
