using Dotnet.Shell.Logic.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class OSTests
    {
        [TestMethod]
        public async Task  GetOSHistoryAsync()
        {
            await OS.GetOSHistoryAsync();
        }
    }
}
