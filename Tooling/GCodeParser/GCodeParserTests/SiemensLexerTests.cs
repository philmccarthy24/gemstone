using Antlr4.Runtime;
using GCodeParser;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GCodeParserTests
{
    [TestFixture]
    public class SiemensLexerTests
    {

        public static IEnumerable<IToken> Tokenise(string testInput)
        {
            AntlrInputStream inputStream = new AntlrInputStream(testInput);
            SiemensGCodeLexer siemensLexer = new SiemensGCodeLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(siemensLexer);

            commonTokenStream.Fill();
            return commonTokenStream.GetTokens();
        }

        [Test]
        public void Test_Lexer_OnFile()
        {
            var testDir = @"C:\Workspace\Testing\SiemensTestFiles";
            var testMacroContent = File.ReadAllText(Path.Combine(testDir, "L9124.SPF"));
            testMacroContent = testMacroContent.Replace("\r", "");

            var tokens = Tokenise(testMacroContent);
            var sb = new StringBuilder();
            foreach (var t in tokens)
            {
                var tokenStr = t.ToString();
                var symbolName = SiemensGCodeLexer.DefaultVocabulary.GetSymbolicName(t.Type);
                tokenStr = Regex.Replace(tokenStr, "<(.+?)>", $"<$1={symbolName}>");
                sb.AppendLine(tokenStr);
            }
            File.WriteAllText(@"E:\Tokenised.txt", sb.ToString());
        }
    }
}
