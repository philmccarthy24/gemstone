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
        /// Tests that incorrect syntax results in a useful error.
        /// </summary>
        [Test]
        public void TestFanucInterpreter_WrongGCodeSyntax_ResultsInUsefulError()
        {
            var ae = Assert.Throws<AggregateException>(() => LoadProgram(@"%
O9874(TestProg)
IF[[#4EQ#0]GOTO30
N30
%"));
            CollectionAssert.IsNotEmpty(ae.InnerExceptions);
            foreach (var ie in ae.InnerExceptions)
            {
                Assert.IsInstanceOf<GCodeException>(ie);
                Assert.That(((GCodeException)ie).Line, Is.EqualTo(3));
            }
        }

        /// <summary>
        /// Tests that basic variable setting works - assigning null, double, 
        /// an int, and the value of another varible. Also that signed literals work
        /// </summary>
        [Test]
        public void TestFanucInterpreter_BasicVariableAssignmentWorks()
        {
            var testProgram = LoadProgram(@"%
O9874(TestProg)
#1=#0
#2=4.9
#3=12
#4=#2
#5=#3
#6=-23.2
#7=+25.7
#8=+3.
#9=-2
%");

            IMachineToolRuntime runtime = new FanucMachineToolRuntime();
            IGCodeInterpreter interpreter = new FanucGCodeInterpreter(runtime);

            interpreter.RunProgram(testProgram);

            Assert.That(interpreter[1], Is.Null);
            Assert.That(interpreter[2], Is.EqualTo(4.9));
            Assert.That(interpreter[3], Is.EqualTo(12));
            Assert.That(interpreter[4], Is.EqualTo(4.9));
            Assert.That(interpreter[5], Is.EqualTo(12));
            Assert.That(interpreter[6], Is.EqualTo(-23.2));
            Assert.That(interpreter[7], Is.EqualTo(25.7));
            Assert.That(interpreter[8], Is.EqualTo(3));
            Assert.That(interpreter[9], Is.EqualTo(-2));
        }

        /// <summary>
        /// Tests that the whole variable range is accessible. Also tests IF, GOTO, 
        /// simple variable indirection and simple variable assignment
        /// </summary>
        [Test]
        public void TestFanucInterpreter_IfGotoAndWorkingVariableRange()
        {
            var testProgram = LoadProgram(@"%
O9874(TestProg)
#33=1
N10
IF[#33GT33]GOTO20
#[#33]=#33
#33=#33+1
GOTO10
N20
#33=33
#199=100
N30
IF[#199GT199]GOTO40
#[#199]=#199
#199=#199+1
GOTO30
N40
#199=199
#999=500
N50
IF[#999GT999]GOTO60
#[#999]=#999
#999=#999+1
GOTO50
N60
#999=999
%");

            IMachineToolRuntime runtime = new FanucMachineToolRuntime();
            IGCodeInterpreter interpreter = new FanucGCodeInterpreter(runtime);

            interpreter.RunProgram(testProgram);

            uint i = 1;
            do
            {
                Assert.That(interpreter[i], Is.EqualTo(i));
                if (i == 33)
                    i = 100;
                else if (i == 199)
                    i = 500;
                else i++;
            } while (i < 1000);
        }

        /// <summary>
        /// Tests that accessing an invalid var or writing to an invalid var results
        /// in a sensible error
        /// </summary>
        [Test]
        public void TestFanucInterpreter_UsingInvalidVars_ResultsInError()
        {
            IMachineToolRuntime runtime = new FanucMachineToolRuntime();
            IGCodeInterpreter interpreter = new FanucGCodeInterpreter(runtime);

            var assignmentToNullTestProgram = LoadProgram(@"%
O9874(TestProg)
#0=42.899
%");
            var invalidAssignmentTestProgram = LoadProgram(@"%
O9874(TestProg)
#237=-99.876
%");
            var invalidAccessTestProgram1 = LoadProgram(@"%
O9874(TestProg)
#2=#460
%");
            var invalidAccessTestProgram2 = LoadProgram(@"%
O9874(TestProg)
#2=#470-5
%");
            var invalidIndirectionTestProgram = LoadProgram(@"%
O9874(TestProg)
#1=-5
#[#1]=56.4
%");
            var ex1 = Assert.Throws<GCodeException>(() => interpreter.RunProgram(assignmentToNullTestProgram));
            Assert.That(ex1.Line, Is.EqualTo(3));
            var ex2 = Assert.Throws<GCodeException>(() => interpreter.RunProgram(invalidAssignmentTestProgram));
            Assert.That(ex2.Line, Is.EqualTo(3));
            var ex3 = Assert.Throws<GCodeException>(() => interpreter.RunProgram(invalidAccessTestProgram1));
            Assert.That(ex3.Line, Is.EqualTo(3));
            var ex4 = Assert.Throws<GCodeException>(() => interpreter.RunProgram(invalidAccessTestProgram2));
            Assert.That(ex4.Line, Is.EqualTo(3));
            var ex5 = Assert.Throws<GCodeException>(() => interpreter.RunProgram(invalidIndirectionTestProgram));
            Assert.That(ex5.Line, Is.EqualTo(4));
        }

        //TODO: write tests to test null handling of relational and arithmetic ops.
        // write test to test null handling wrt var assignmnent (from expression vs direct from var)
        // write test to prove operator precedence is correct.
        
        // write test to prove IF THEN, IF GOTO and GOTO are correct
        // write test to prove IF with a non-relational condition expression barfs properly

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