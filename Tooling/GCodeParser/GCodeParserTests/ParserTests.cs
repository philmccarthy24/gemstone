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
        public void TestSyntaxCheck()
        {
            var syntaxChecker = new FanucGCodeSyntaxChecker();

            var testGCode = @"(LABEL_MINPUTVALID2D)
N20
IF[#26NE#0]GOTO10
#3000=3000(118 INVALID Z PARAM - MUST SPECIFY Z FOR 2D CALIBRATION)
(LABEL_MINPUTVALID)
N10";
            syntaxChecker.CheckSyntax(testGCode);
        }
    }
}
