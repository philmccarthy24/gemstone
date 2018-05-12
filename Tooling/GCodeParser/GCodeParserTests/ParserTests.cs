using GCodeParser;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCodeParserTests
{

    [TestFixture]
    public class ParserTests
    {

        [Test]
        public void TestExtraBracket()
        {
            var syntaxChecker = new FanucGCodeSyntaxChecker();

            var testGCode = "IF[#26NE#0]]GOTO10\n";

            var errors = syntaxChecker.CheckSyntax(testGCode);

            Assert.That(errors.Count, Is.GreaterThan(0));
        }

        [Test]
        public void TestValidText()
        {
            var syntaxChecker = new FanucGCodeSyntaxChecker();

            var testGCode = "G01X5Y#[#26-4]\n"; // if add '**' before newline, get errors about gcode prefix... ? also getting error about empty block. Parser grammar is not quite right.

            var errors = syntaxChecker.CheckSyntax(testGCode);

            Assert.That(errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestSingleCommentLine()
        {
            var syntaxChecker = new FanucGCodeSyntaxChecker();

            var testGCode = "(Just a comment)\n";

            var errors = syntaxChecker.CheckSyntax(testGCode);

            Assert.That(errors.Count, Is.EqualTo(0));
        }
    }
}
