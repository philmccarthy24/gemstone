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
            string testProgText = @"%
O9874(TestProg)
IF[4.87GT2.91]GOTO20
N10 (This is label 10)
N20 (This is label 20)
N30 (This is label 30)
%";
            var testProgram = new FanucGCodeProgram() { Content = testProgText };

            IMachineToolRuntime runtime = new FanucMachineToolRuntime();
            IGCodeInterpreter interpreter = new FanucGCodeInterpreter(runtime);

            interpreter.RunProgram(testProgram);

            //Assert.That(errors.Count, Is.GreaterThan(0));
        }
    }
}