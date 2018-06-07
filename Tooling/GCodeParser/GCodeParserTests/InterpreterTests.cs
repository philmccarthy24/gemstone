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
    public class FanucGCodeInterpreterTests
    {

        [Test]
        public void TestSimpleProgram()
        {
            // this tests that it's possible to have empty blocks, blocks with just a sequence number,
            // blocks with just a comment, and blocks with a sequence number and a comment.
            //
            string testProgText = @"%
O9874(TestProg)
N10

(Just a comment)
IF[4.87GT2.91]GOTO30
N20 (This is label 10)
N30 (This is label 20)
N40 (This is label 30)
%";
            testProgText = testProgText.Replace("\r", "");

            var testProgram = new FanucGCodeProgram() { Content = testProgText };

            IMachineToolRuntime runtime = new FanucMachineToolRuntime();
            IGCodeInterpreter interpreter = new FanucGCodeInterpreter(runtime);

            interpreter.RunProgram(testProgram);

            //Assert.That(errors.Count, Is.GreaterThan(0));
        }
    }
}