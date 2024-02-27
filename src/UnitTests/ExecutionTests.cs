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
    public sealed class ExecutionTests
    {
        [TestMethod]
        public async Task ConstructAsync()
        {
            await ShouldNotThrow(async () =>
            {
                var errorDisplay = new ErrorDisplay(new AssertingConsole());
                _ = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
            });
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
                // ignore
            }
            finally
            {
                File.Delete(emptyFile);
            }
        }

        [TestMethod]
        public async Task LoadAssemblyFromFile_DLLAsync()
        {
            await ShouldNotThrow(async () =>
            {
                var errorDisplay = new ErrorDisplay(new AssertingConsole());
                var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
                await exe.LoadAssemblyFromFileAsync(Assembly.GetExecutingAssembly().Location);
            });
        }

        [TestMethod]
        public async Task Load_nshAsync()
        {
            await ShouldNotThrow(async () =>
            {
                var errorDisplay = new ErrorDisplay(new AssertingConsole());
                var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
                await exe.ExecuteFileAsync(@".\TestFiles\nshScriptTest.nsh".Replace('\\', Path.DirectorySeparatorChar));
            });
        }

        [TestMethod]
        public async Task Load_CSAsync()
        {
            await ShouldNotThrow(async () =>
            {
                var errorDisplay = new ErrorDisplay(new AssertingConsole());
                var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
                await exe.ExecuteFileAsync(@".\TestFiles\csScriptTest.cs".Replace('\\', Path.DirectorySeparatorChar));
            });
        }

        [TestMethod]
        public async Task AccessShellAPIAsync()
        {
            await ShouldNotThrow(async () =>
            {
                var errorDisplay = new ErrorDisplay(new AssertingConsole());
                var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
                await exe.ExecuteAsync("Console.WriteLine( Shell.WorkingDirectory );");
            });
        }

        [TestMethod]
        public async Task AccessColorStringAsync()
        {
            await ShouldNotThrow(async () =>
            {
                var errorDisplay = new ErrorDisplay(new AssertingConsole());
                var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
                await exe.ExecuteAsync("using System.Drawing; Console.WriteLine( new ColorString(\"Hello\", Color.Red) );");
            });
        }
        [TestMethod]
        public async Task AccessConsoleExAsync()
        {
            await ShouldNotThrow(async () =>
            {
                var errorDisplay = new ErrorDisplay(new AssertingConsole());
                var exe = await ShellExecutor.GetDefaultExecuterAsync(errorDisplay);
                await exe.ExecuteAsync("using System.Drawing; using Dotnet.Shell.API.Helpers; ConsoleEx.WriteLine(\"Hello\", Color.Red);");
            });
        }

        private static async Task ShouldNotThrow(Func<Task> action)
        {
            try
            {
                if (action != null)
                {
                    await action();
                    return;
                }

                throw new NullReferenceException(nameof(action));
            }
            catch (Exception ex)
            {
                Assert.Fail("Expected no exception, but got: " + ex.Message);
            }
        }
    }
}
