using Dotnet.Shell.API;
using Dotnet.Shell.Logic.Suggestions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class CmdSuggestionsTests
    {
        private string basePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestFiles");

        [TestInitialize]
        public void testInit()
        {
            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

            File.Create(Path.Combine(basePath, "atestcmd" + extension)).Dispose();
            File.Create(Path.Combine(basePath, "btestcmd" + extension)).Dispose();

            File.Create(Path.Combine(basePath, "testcmdc" + extension)).Dispose();
            File.Create(Path.Combine(basePath, "testcmdd" + extension)).Dispose();
        }

        [TestCleanup]
        public void testClean()
        {
            foreach (var file in Directory.GetFiles(basePath, "*testcmd*"))
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void Construct()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var s = new CmdSuggestions(fakeShell);
            }
        }

        [TestMethod]
        public async Task SimpleQueryWithSingleResultAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.Paths.Add(basePath);

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("a", 1);

                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("testcmd", result[0].CompletionText);
                Assert.AreEqual(1, result[0].Index);

                result = await s.GetSuggestionsAsync("b", 1);

                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("testcmd", result[0].CompletionText);
                Assert.AreEqual(1, result[0].Index);
            }
        }

        [TestMethod]
        public async Task SimpleQueryWithoutResultAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.Paths.Add(basePath);

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("c", 1);

                Assert.AreEqual(0, result.Count);
            }
        }

        [TestMethod]
        public async Task QueryWithMultipleResultsAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.Paths.Add(basePath);

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var results = await s.GetSuggestionsAsync("test", 4);

                Assert.AreEqual(2, results.Count);

                foreach (var result in results)
                {
                    Assert.IsTrue(result.CompletionText == "cmdc" || result.CompletionText == "cmdd");
                    Assert.AreEqual(4, result.Index);
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

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("echo a; b; echo c; echo a", 9);

                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("testcmd", result[0].CompletionText);
                Assert.AreEqual(9, result[0].Index);
            }
        }

        [TestMethod]
        public async Task CompleteDirectoryAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.ChangeDir( Path.GetFullPath(basePath + "/../") );

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("cd T", 4);

                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("estFiles"+Path.DirectorySeparatorChar, result[0].CompletionText);
                Assert.AreEqual(4, result[0].Index);
            }
        }

        [TestMethod]
        public async Task CompleteDirectory_NotFoundAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.ChangeDir(Path.GetFullPath(basePath + "/../"));

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("cd X", 4);

                Assert.AreEqual(0, result.Count);
            }
        }

        [TestMethod]
        public async Task CompleteDirectory_WithinLargerCommandAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.ChangeDir(Path.GetFullPath(basePath + "/../"));

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("echo A; cd T", 12);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("estFiles\\", result[0].CompletionText);
                Assert.AreEqual(12, result[0].Index);

                result = await s.GetSuggestionsAsync("echo A;cd T", 11);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("estFiles\\", result[0].CompletionText);
                Assert.AreEqual(11, result[0].Index);

                result = await s.GetSuggestionsAsync("echo A && cd T", 14);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("estFiles\\", result[0].CompletionText);
                Assert.AreEqual(14, result[0].Index);

                result = await s.GetSuggestionsAsync("cd T && cd A", 4);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("estFiles\\", result[0].CompletionText);
                Assert.AreEqual(4, result[0].Index);
            }
        }

        [TestMethod]
        public async Task CompleteFileAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.ChangeDir(Path.GetFullPath(basePath + "/../"));

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("cat TestFi", 10);

                Assert.AreEqual(1, result.Count);
            }
        }

        [TestMethod]
        public void InternalFileResolution_ConvertToAbsolute()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.HomeDirectory = basePath;
                fakeShell.WorkingDirectory = basePath;

                CmdSuggestions s = new CmdSuggestions(fakeShell);

                Assert.AreEqual(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), s.ConvertToAbsolute(basePath + "/.."));
                Assert.AreEqual(fakeShell.HomeDirectory + Path.DirectorySeparatorChar, s.ConvertToAbsolute("~/"));
                Assert.AreEqual(fakeShell.WorkingDirectory + Path.DirectorySeparatorChar, s.ConvertToAbsolute("./"));
                Assert.AreEqual(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "bob"), s.ConvertToAbsolute("~/../bob"));
            }
        }

        [TestMethod]
        public async Task CompleteFilesInDirAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.HomeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                fakeShell.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("cat TestFiles/nsh", 17);

                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("ScriptTest.nsh", result[0].CompletionText);
                Assert.AreEqual(17, result[0].Index);
            }
        }

        [TestMethod]
        public async Task CompleteExecutableInDirAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.HomeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                fakeShell.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("."+Path.DirectorySeparatorChar+"TestFiles"+Path.DirectorySeparatorChar+"nshScr", 18);

                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("iptTest.nsh", result[0].CompletionText);
                Assert.AreEqual(18, result[0].Index);
            }
        }

        [TestMethod]
        public async Task CompleteExecutableInCurrentDirAsync()
        {
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                fakeShell.HomeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                fakeShell.WorkingDirectory = Path.Combine( Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestFiles");

                CmdSuggestions s = new CmdSuggestions(fakeShell);
                var result = await s.GetSuggestionsAsync("." + Path.DirectorySeparatorChar + "nshScr", 8);

                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("iptTest.nsh", result[0].CompletionText);
                Assert.AreEqual(8, result[0].Index);
            }
        }
    }
}
