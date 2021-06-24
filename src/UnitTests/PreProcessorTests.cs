using Dotnet.Shell.API;
using Dotnet.Shell.Logic.Compilation;
using Dotnet.Shell.Logic.Compilation.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class PreProcessorTests
    {
        [TestMethod]
        public async Task VariableAssignmentAsync()
        {
            var testString = "var data=`cat /etc/passwd`;";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(testString);
                Assert.AreEqual("var data=Shell.Execute(\"cat /etc/passwd\", Redirection.Out).ConvertStdOutToVariable<string>();", result);
            }
        }

        [TestMethod]
        public async Task VerbatimRegionCSharpAsync()
        {
            var script = @"#region c#
everything
here is
verbatim
var data = ""cat /etc/passwd""
echo hello
#endregion";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(script);

                var scriptAsString = script.Replace("#region c#", string.Empty).Replace("#endregion", string.Empty).Trim().Replace("\r\n", "LINE").Replace("\n", "LINE");
                Assert.AreEqual(scriptAsString, result.Replace("\r\n", "LINE").Replace("\n", "LINE").Trim());
            }
        }

        [TestMethod]
        public async Task SingleCommandAsync()
        {
            var commands = new string[]
            {
                "cat /etc/passwd",
                "sdsd .nsh"
            };

            foreach (var cmd in commands)
            {
                using (var ms = new MemoryStream())
                {
                    var fakeShell = new Shell();
                    var result = await(new SourceProcessor()).ProcessAsync(cmd);
                    Assert.AreEqual("_= await Shell.ExecuteAsync(\"" + cmd+"\");", result);
                }
            }
        }

        [TestMethod]
        public async Task ChangeDirAsync()
        {
            var commands = new string[]
            {
                "cd ~",
                "cd /tmp"
            };

            foreach (var cmd in commands)
            {
                using (var ms = new MemoryStream())
                {
                    var fakeShell = new Shell();
                    var result = await (new SourceProcessor()).ProcessAsync(cmd);
                    Assert.AreEqual("Shell.ChangeDir(\""+ cmd.Remove(0, 3) + "\");", result);
                }
            }
        }

        [TestMethod]
        public async Task VerbatimRegionCmd_SingleLineAsync()
        {
            var script = @"
        #region cmd
        echo ""hello""
        #endregion";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(script);

                Assert.AreEqual("_= await Shell.ExecuteAsync(\"echo \\\"hello\\\"\");", result);
            }
        }


        [TestMethod]
        [ExpectedException(typeof(PreProcessorSyntaxException))]
        public async Task VariableAssignment_Error_NoSemiColonAsync()
        {
            var testString = "var data=`cat /etc/passwd`";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(testString);
            }
        }

        [TestMethod]
        public async Task LoadWithArgAsync()
        {
            var testString = "#load $someArg$";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("await Shell.LoadScriptFromFileAsync(Shell.ConvertPathToAbsolute(someArg));", result);
            }
        }

        [TestMethod]
        public async Task VariableAssignmentWithVariableCaptureAsync()
        {
            var script = @"
var testNum = 5;
var testStr = ""hello"";
var exec=`cat $testNum$ $testStr$`;";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(script);

                var output = result.Split(Environment.NewLine);
                Assert.AreEqual(script.Trim().Split(Environment.NewLine)[0].Trim(), output[0].Trim());
                Assert.AreEqual(script.Trim().Split(Environment.NewLine)[1].Trim(), output[1].Trim());
                Assert.AreEqual("var exec=Shell.Execute(\"cat \"+testNum+\" \"+testStr, Redirection.Out).ConvertStdOutToVariable<string>();", output[2].Trim());
            }
        }

        [TestMethod]
        public async Task VariableAssignment_Regression_2Async()
        {
            var testString = "var commandOutput=`$command$`;";
            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("var commandOutput=Shell.Execute(command, Redirection.Out).ConvertStdOutToVariable<string>();", result);
            }
        }

        [TestMethod]
        public async Task VariableAssignment_Regression_3Async()
        {
            var testString = "string powerLinePrompt=`powerline-render shell left $Shell.LastExitCode$`;";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("string powerLinePrompt=Shell.Execute(\"powerline-render shell left \"+Shell.LastExitCode, Redirection.Out).ConvertStdOutToVariable<string>();", result);
            }
        }

        [TestMethod]
        public async Task VariableAssignment_Regression_4Async()
        {
            var testString = "var repository = Repository.Factory.GetCoreV3(\"https://api.nuget.org/v3/index.json\");";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual(testString, result);
            }
        }

        [TestMethod]
        public async Task VariableAssignment_Regression_5Async()
        {
            var testString = "List<string> z=`dmesg`;";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await (new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("List<string> z=Shell.Execute(\"dmesg\", Redirection.Out).ConvertStdOutToVariable<List<string>>();", result);
            }
        }

        [TestMethod]
        public async Task StripComments_SingleLineAsync()
        {
            var testString = "var uri=\"http://test\"; // this is a test";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("var uri = \"http://test\";", result);
            }
        }

        [TestMethod]
        public async Task StripComments_TrimWhiteSpaceAsync()
        {
            var testString = "   var x = 1;   ";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("var x = 1;", result);
            }
        }

        [TestMethod]
        public void BackTickCommand_InternalExtractions()
        {
            BacktickCommand cmd = new();
            var result = cmd.GetBacktickedCommands("`test`");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Item1);
            Assert.AreEqual(4, result[0].Item2);

            // multiple
            result = cmd.GetBacktickedCommands("`test` `another`");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].Item1);
            Assert.AreEqual(4, result[0].Item2);
            Assert.AreEqual(8, result[1].Item1);
            Assert.AreEqual(7, result[1].Item2);

            // not in a string
            result = cmd.GetBacktickedCommands("\"`test` `another`\"");
            Assert.AreEqual(0, result.Count);

            // not escaped
            result = cmd.GetBacktickedCommands("`test` \\`another\\`");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Item1);
            Assert.AreEqual(4, result[0].Item2);

            // included in a string
            result = cmd.GetBacktickedCommands("\"`test`\"");
            Assert.AreEqual(0, result.Count);

            result = cmd.GetBacktickedCommands("this is a \"`test`\"");
            Assert.AreEqual(0, result.Count);

            result = cmd.GetBacktickedCommands("\"`test`\" `1234`");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(10, result[0].Item1);
            Assert.AreEqual(4, result[0].Item2);
        }

        [TestMethod]
        public void BackTickCommand_Parsing()
        {
            BacktickCommand cmd = new();
            Assert.IsTrue(cmd.IsValid("test `123`"));
            Assert.IsTrue(cmd.IsValid("test `"));

            var ret = cmd.GetMetaRepresentation("var x = `echo 1` == 1;");
            Assert.AreEqual("#region SUBCOMMAND // var x = Shell.Execute(\"echo 1\", Redirection.Out).ConvertStdOutToVariable<string>() == 1; #endregion", ret);

            ret = cmd.GetMetaRepresentation("`ps aux`.Split(Environment.Newline).Distinct();");
            Assert.AreEqual("#region SUBCOMMAND // Shell.Execute(\"ps aux\", Redirection.Out).ConvertStdOutToVariable<string>().Split(Environment.Newline).Distinct(); #endregion", ret);

            ret = cmd.GetMetaRepresentation("bool x = string.IsNullEmptyOrWhitespace(`cmd`);");
            Assert.AreEqual("#region SUBCOMMAND // bool x = string.IsNullEmptyOrWhitespace(Shell.Execute(\"cmd\", Redirection.Out).ConvertStdOutToVariable<string>()); #endregion", ret);

            ret = cmd.GetMetaRepresentation("DirectoryInfo dir = `/bin/echo /bin/`;");
            Assert.AreEqual("#region SUBCOMMAND // DirectoryInfo dir = Shell.Execute(\"/bin/echo /bin/\", Redirection.Out).ConvertStdOutToVariable<DirectoryInfo>(); #endregion", ret);

            ret = cmd.GetMetaRepresentation("Stream stream = `cat CmdSyntax.nsh`;");
            Assert.AreEqual("#region SUBCOMMAND // Stream stream = Shell.Execute(\"cat CmdSyntax.nsh\", Redirection.Out).ConvertStdOutToVariable<Stream>(); #endregion", ret);
        }

        [TestMethod]
        public async Task CommandHandlingRegression_1_Async()
        {
            var testString = "./hello";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await(new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("_= await Shell.ExecuteAsync(\"./hello\");", result);
            }
        }

        [TestMethod]
        public async Task CommandHandlingCommentRegression_1_Async()
        {
            var testString = "wget http://127.0.0.1/test/123/";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await (new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("_= await Shell.ExecuteAsync(\"wget http://127.0.0.1/test/123/\");", result);
            }
        }

        [TestMethod]
        public async Task CommandHandlingRegression_2_Async()
        {
            var testString = "wget https://svn.nmap.org/nmap/scripts/test.nse";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await (new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("_= await Shell.ExecuteAsync(\"wget https://svn.nmap.org/nmap/scripts/test.nse\");", result);
            }
        }

        [TestMethod]
        public async Task CommandHandlingRegexMatchRegression_1_Async()
        {
            var testString = "ssh-keygen -f /home/test/.ssh/known_hosts -R 10.0.0.0";

            using (var ms = new MemoryStream())
            {
                var fakeShell = new Shell();
                var result = await (new SourceProcessor()).ProcessAsync(testString);

                Assert.AreEqual("_= await Shell.ExecuteAsync(\"ssh-keygen -f /home/test/.ssh/known_hosts -R 10.0.0.0\");", result);
            }
        }
    }
}
