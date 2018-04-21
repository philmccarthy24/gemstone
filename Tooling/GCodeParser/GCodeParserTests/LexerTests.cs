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

        /// <summary>
        /// Verifies that the ControlOut lexical mode activates on '(', and
        /// other tokens appearing in the comment text do not get tokenised
        /// by the lexer
        /// </summary>
        [Test]
        public void TestCommentLexicalMode()
        {
            var tokens = FanucGCodeScanner.Tokenise("N70 ( test N50 comment #_UI[9] also #104 IF THEN )\n");
            var test = tokens.ToList();

            var expected = new List<FanucGCodeTokenTypes>() {
                FanucGCodeTokenTypes.LabelPrefix,
                FanucGCodeTokenTypes.Integer,
                FanucGCodeTokenTypes.CommentStart,
                FanucGCodeTokenTypes.CommentText,
                FanucGCodeTokenTypes.CommentEnd,
                FanucGCodeTokenTypes.EndOfBlock,
            };

            CollectionAssert.AreEqual(expected, tokens.Select(t => t.TokenType));
        }

        [Test]
        public void TestSystemVarIdentifier()
        {
            var testInput = @"%
O9123(MyProg)
G01X[#5-20.5] Y[#_ALS[4] ]
";
            var tokens = FanucGCodeScanner.Tokenise(testInput);
            var test = tokens.ToList();

            var expected = new List<FanucGCodeTokenTypes>() {
                FanucGCodeTokenTypes.StartEndProgram,
                FanucGCodeTokenTypes.EndOfBlock,
                FanucGCodeTokenTypes.ProgramNumberPrefix,
                FanucGCodeTokenTypes.Integer,
                FanucGCodeTokenTypes.CommentStart,
                FanucGCodeTokenTypes.CommentText,
                FanucGCodeTokenTypes.CommentEnd,
                FanucGCodeTokenTypes.EndOfBlock,
                FanucGCodeTokenTypes.GCodePrefix,
                FanucGCodeTokenTypes.Integer,
                FanucGCodeTokenTypes.GCodePrefix,
                FanucGCodeTokenTypes.OpenBracket,
                FanucGCodeTokenTypes.Hash,
                FanucGCodeTokenTypes.Integer,
                FanucGCodeTokenTypes.Minus,
                FanucGCodeTokenTypes.Decimal,
                FanucGCodeTokenTypes.CloseBracket,
                FanucGCodeTokenTypes.GCodePrefix,
                FanucGCodeTokenTypes.OpenBracket,
                FanucGCodeTokenTypes.Hash,
                FanucGCodeTokenTypes.NamedVariable,
                FanucGCodeTokenTypes.OpenBracket,
                FanucGCodeTokenTypes.Integer,
                FanucGCodeTokenTypes.CloseBracket,
                FanucGCodeTokenTypes.CloseBracket,
                FanucGCodeTokenTypes.EndOfBlock,
            };

            CollectionAssert.AreEqual(expected, tokens.Select(t => t.TokenType));
        }

        [Test]
        public void TestNumberInBrackets()
        {
            var tokens = FanucGCodeScanner.Tokenise("G1X[#[100]]\n");
            var test = tokens.ToList();

            var expected = new List<FanucGCodeTokenTypes>() {
                    FanucGCodeTokenTypes.GCodePrefix,
                    FanucGCodeTokenTypes.Integer,
                    FanucGCodeTokenTypes.GCodePrefix,
                    FanucGCodeTokenTypes.OpenBracket,
                    FanucGCodeTokenTypes.Hash,
                    FanucGCodeTokenTypes.OpenBracket,
                    FanucGCodeTokenTypes.Integer,
                    FanucGCodeTokenTypes.CloseBracket,
                    FanucGCodeTokenTypes.CloseBracket,
                    FanucGCodeTokenTypes.EndOfBlock,
                };

            CollectionAssert.AreEqual(expected, tokens.Select(t => t.TokenType));
        }

    }
}
