using GCode.Interpreter;
using GCode.Utility;
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
        private FanucGCodeProgram LoadProgram(string text)
        {
            return new FanucGCodeProgram() { Content = text.Replace("\r", "") };
        }

        /// <summary>
        /// Test that it's possible to have empty blocks, blocks with just a sequence number,
        /// blocks with just a comment, and blocks with a sequence number and a comment.
        /// </summary>
        [Test]
        public void TestFanucInterpreter_EmptyBlocks()
        {
            var testProgram = LoadProgram(@"%
O9874(TestProg)
N10

(Just a comment)
N20 (This is label 20)
N30
N40 (This is label 40)
%");

            IMachineToolRuntime runtime = new FanucMachineToolRuntime();
            IGCodeInterpreter interpreter = new FanucGCodeInterpreter(runtime);

            Assert.DoesNotThrow(() => interpreter.RunProgram(testProgram));
        }

        /// <summary>
        /// Test that incorrect syntax results in an useful error.
        /// </summary>
        [Test]
        public void TestFanucInterpreter_WrongSyntax()
        {
            Assert.Throws<AggregateException>(() => LoadProgram(@"%
O9874(TestProg)
IF[[#4EQ#0]GOTO30
N30
%"));
        }

        /// <summary>
        /// Tests that basic variable setting works - assigning null, double, 
        /// an int, and the value of another varible
        /// </summary>
        [Test]
        public void TestFanucInterpreter_BasicVariableAssignment()
        {
            var testProgram = LoadProgram(@"%
O9874(TestProg)
#1=#0
#2=4.9
#3=12
#4=#2
%");

            IMachineToolRuntime runtime = new FanucMachineToolRuntime();
            IGCodeInterpreter interpreter = new FanucGCodeInterpreter(runtime);

            interpreter.RunProgram(testProgram);

            Assert.That(interpreter[1], Is.Null);
            Assert.That(interpreter[2], Is.EqualTo(4.9));
            Assert.That(interpreter[3], Is.EqualTo(12));
            Assert.That(interpreter[4], Is.EqualTo(4.9));
        }

        //TODO: write tests to test null handling of relational and arithmetic ops.
        // write test to test null handling wrt var assignmnent (from expression vs direct from var)
        // write test to prove operator precedence is correct.
        // write test to prove we get a sensible exception for assigning to #0. (check lin num?)
        // write test to prove IF THEN, IF GOTO and GOTO are correct
        // write test to prove IF with a non-relational condition expression barfs properly
        // update syntax error test - test single vs aggregate exception, check that line nums correct in exception?

        /*
        [Test]
        public void TestFanucInterpreter_()
        {
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
        */
        
    }
}