using Dotnet.Shell.Logic.Compilation;
using Dotnet.Shell.UI;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class ExecutionTests
    {
        [TestMethod]
        public async Task ConstructAsync()
        {
            var errorDisplay = new ErrorDisplay(new AssertingConsole());
            _ = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public async Task LoadAssemblyFromFile_DLL_InvalidAsync()
        {
            var errorDisplay = new ErrorDisplay(new AssertingConsole());
            var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
            await exe.LoadAssemblyFromFileAsync("");
            await exe.ExecuteAsync(string.Empty);
        }

        [TestMethod]
        public async Task LoadAssemblyFromFile_DLL_MissingAsync()
        {
            var emptyFile = Path.GetTempFileName();
            try
            {
                var errorDisplay = new ErrorDisplay(new AssertingConsole());
                var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
                await exe.LoadAssemblyFromFileAsync(emptyFile);
                await exe.ExecuteAsync(string.Empty);
                Assert.Fail();
            }
            catch (CompilationErrorException)
            {
            }
            finally
            {
                File.Delete(emptyFile);
            }
        }

        [TestMethod]
        public async Task LoadAssemblyFromFile_DLLAsync()
        {
            var errorDisplay = new ErrorDisplay(new AssertingConsole());
            var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
            await exe.LoadAssemblyFromFileAsync(Assembly.GetExecutingAssembly().Location);
        }

        [TestMethod]
        public async Task Load_nshAsync()
        {
            var errorDisplay = new ErrorDisplay(new AssertingConsole());
            var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
            await exe.ExecuteFileAsync(@".\TestFiles\nshScriptTest.nsh".Replace('\\', Path.DirectorySeparatorChar));
        }

        [TestMethod]
        public async Task Load_CSAsync()
        {
            var errorDisplay = new ErrorDisplay(new AssertingConsole());
            var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
            await exe.ExecuteFileAsync(@".\TestFiles\csScriptTest.cs".Replace('\\', Path.DirectorySeparatorChar));
        }

        [TestMethod]
        public async Task AccessShellAPIAsync()
        {
            var errorDisplay = new ErrorDisplay(new AssertingConsole());
            var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
            await exe.ExecuteAsync("Console.WriteLine( Shell.WorkingDirectory );");
        }

        [TestMethod]
        public async Task AccessColorStringAsync()
        {
            var errorDisplay = new ErrorDisplay(new AssertingConsole());
            var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
            await exe.ExecuteAsync("using System.Drawing; Console.WriteLine( new ColorString(\"Hello\", Color.Red) );");
        }
    }
}
