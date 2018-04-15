using Antlr4.Runtime;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GCodeParser;

namespace GCodeParserTests
{
    [TestFixture]
    public class LexerTests
    {
        private IList<IToken> Tokenise(string testInput)
        {
            AntlrInputStream inputStream = new AntlrInputStream(testInput);
            FanucGCodeLexer fanucLexer = new FanucGCodeLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(fanucLexer);

            commonTokenStream.Fill();
            return commonTokenStream.GetTokens();
        }

        /// <summary>
        /// Verifies that the ControlOut lexical mode activates on '(', and
        /// other tokens appearing in the comment text do not get tokenised
        /// by the lexer
        /// </summary>
        [Test]
        public void TestCommentLexicalMode()
        {
            var tokens = Tokenise("N70 ( test N50 comment #_UI[9] also #104 IF THEN )\n");
            var test = tokens.ToList();

            var expected = new List<int>() {
                GCodeParser.FanucGCodeLexer.SEQUENCE_NUMBER_PREFIX,
                GCodeParser.FanucGCodeLexer.INTEGER,
                GCodeParser.FanucGCodeLexer.CTRL_OUT,
                GCodeParser.FanucGCodeLexer.CTRL_OUT_TEXT,
                GCodeParser.FanucGCodeLexer.CTRL_IN,
                GCodeParser.FanucGCodeLexer.EOB,
                GCodeParser.FanucGCodeLexer.Eof };

            CollectionAssert.AreEqual(expected, tokens.Select(t => t.Type));
        }

        [Test]
        public void TestSystemVarIdentifier()
        {
            var testInput = @"%
O9123(MyProg)
G01X[#5-20.5] Y[#_ALS[4] ]
";
            var tokens = Tokenise(testInput);
            var test = tokens.ToList();

            var expected = new List<int>() {
                GCodeParser.FanucGCodeLexer.START_END_PROGRAM,
                GCodeParser.FanucGCodeLexer.EOB,
                GCodeParser.FanucGCodeLexer.PROGRAM_NUMBER_PREFIX,
                GCodeParser.FanucGCodeLexer.INTEGER,
                GCodeParser.FanucGCodeLexer.CTRL_OUT,
                GCodeParser.FanucGCodeLexer.CTRL_OUT_TEXT,
                GCodeParser.FanucGCodeLexer.CTRL_IN,
                GCodeParser.FanucGCodeLexer.EOB,
                GCodeParser.FanucGCodeLexer.GCODE_PREFIX,
                GCodeParser.FanucGCodeLexer.INTEGER,
                GCodeParser.FanucGCodeLexer.GCODE_PREFIX,
                GCodeParser.FanucGCodeLexer.OPEN_BRACKET,
                GCodeParser.FanucGCodeLexer.HASH,
                GCodeParser.FanucGCodeLexer.INTEGER,
                GCodeParser.FanucGCodeLexer.MINUS,
                GCodeParser.FanucGCodeLexer.DECIMAL,
                GCodeParser.FanucGCodeLexer.CLOSE_BRACKET,
                GCodeParser.FanucGCodeLexer.GCODE_PREFIX,
                GCodeParser.FanucGCodeLexer.OPEN_BRACKET,
                GCodeParser.FanucGCodeLexer.HASH,
                GCodeParser.FanucGCodeLexer.SYSTEMVAR_CONST_OR_COMMONVAR_IDENTIFIER,
                GCodeParser.FanucGCodeLexer.OPEN_BRACKET,
                GCodeParser.FanucGCodeLexer.INTEGER,
                GCodeParser.FanucGCodeLexer.CLOSE_BRACKET,
                GCodeParser.FanucGCodeLexer.CLOSE_BRACKET,
                GCodeParser.FanucGCodeLexer.EOB,
                GCodeParser.FanucGCodeLexer.Eof };

            CollectionAssert.AreEqual(expected, tokens.Select(t => t.Type));
        }
    }
}
