using Dotnet.Shell.Logic.Suggestions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class CSharpSuggestionsTests
    {
        [TestMethod]
        public async Task ConstructAsync()
        {
            CSharpSuggestions cSharpSuggestions = new CSharpSuggestions();
            var results = await cSharpSuggestions.GetSuggestionsAsync(string.Empty, 0);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public async Task SimpleQueryAsync()
        {
            CSharpSuggestions cSharpSuggestions = new CSharpSuggestions();

            var text = "Console.WriteLi";

            var results = await cSharpSuggestions.GetSuggestionsAsync(text, text.Length);

            Assert.AreEqual(1, results.Count);
        }

        [TestMethod]
        public async Task SimpleQueryWithMultipleResultsAsync()
        {
            CSharpSuggestions cSharpSuggestions = new CSharpSuggestions();

            var text = "Console.";

            var results = await cSharpSuggestions.GetSuggestionsAsync(text, text.Length);

            Assert.AreEqual(22, results.Count);
        }
    }
}
