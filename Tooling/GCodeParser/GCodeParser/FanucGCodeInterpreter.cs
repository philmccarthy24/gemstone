using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime;
using System.Linq.Expressions;
using System.Reflection;
using GCodeParser;
using Antlr4.Runtime.Tree;

namespace GCodeParser
{

    public interface IMachineToolRuntime
    {
        double Feedrate { get; set; }

        // TODO: add functions to drive simulated machine tool here
    }

    public interface IGCodeInterpreter
    {
        // TODO: add some way of querying local and common variables here

        IMachineToolRuntime Runtime { get; set; }

        void RunProgram(IGCodeProgram startProgram);
    }

    public class FanucMachineToolRuntime : IMachineToolRuntime
    {

        public double Feedrate { get; set; }

        /*
        public double? GetVariable(uint index)
        {
            double? var = null;
            if (index > 0 && index <= NUM_LOCALS)
                var = _localsStack.Peek()[index - 1];
            else if (index < 1000)
                var = _commonVariables[index - NUM_LOCALS + 1];
            else throw new Exception("Bad variable number");
            return var;
        }

        public void SetVariable(uint index, double value)
        {
            if (index == 0)
                throw new Exception("Error: Cannot set #0");

            if (index <= NUM_LOCALS)
                _localsStack.Peek()[index - 1] = value;
            else if (index < 1000)
                _commonVariables[index - NUM_LOCALS + 1] = value;
            else throw new Exception("Error: Attempt to set a variable outside supported limits");
        }
        */
    }

    internal static class ObjectExtenions
    {
        public static bool IsNumeric(this object objToTest)
        {
            return objToTest != null && (objToTest.GetType() == typeof(int) || objToTest.GetType() == typeof(double));
        }
    }
    

    public class FanucGCodeInterpreter : FanucGCodeParserBaseVisitor<object>, IGCodeInterpreter
    {
        private Stack<IGCodeProgram> _stack;
        private double?[] _commonVariables = new double?[500];

        public IMachineToolRuntime Runtime { get; set; }

        public FanucGCodeInterpreter(IMachineToolRuntime runtime)
        {
            Runtime = runtime;
        }

        // TODO: consider MDI mode, or M?? that pauses program. Will need a way of returning
        // block that was last executed, and a way of continuing. Maybe also an event that
        // is fired on block exit for GUIs to plug into, event that is fired on var update (INotifyPropertyChanged?) etc.
        public void RunProgram(IGCodeProgram startProgram)
        {
            _stack = new Stack<IGCodeProgram>();
            _stack.Push(startProgram);
            Visit(startProgram.RunContext);
        }

        // Visitor top level program rule/node handler. Runs the program evaluating each block at a time
        public override object VisitProgram([NotNull] FanucGCodeParser.ProgramContext context)
        {
            // interpret the blocks of the gcode program
            var blockContexts = context.programContent().block();
            do
            {
                uint currBlockPtr = _stack.Peek().CurrentBlockIndex;

                VisitBlock(blockContexts[currBlockPtr]);

                if (currBlockPtr == _stack.Peek().CurrentBlockIndex)
                    // ptr was unmodified by block, so go to next block
                    _stack.Peek().CurrentBlockIndex++;

            } while (_stack.Peek().CurrentBlockIndex < blockContexts.Length);

            return null;
        }

        // Visit a gcode block
        public override object VisitBlock([NotNull] FanucGCodeParser.BlockContext context)
        {
            var statement = context.blockContent().statement();

            // are we dealing with a statement, with a set of gcode addresses and parameter? if so, work out what the IMachineToolRuntime command should be, and execute it.
            // resolve sub-expressions to doubles or integers. error out if they can't be resolved to these.
            if (statement != null && statement.gcode() != null)
            {
                // build a map of gcode prefixes (note multiple codes may be specified in certain cases, eg multiple preparatory 'G' addresses)
                var gcodePrefixMap = new Dictionary<string, List<double>>();
                foreach (var gcode in statement.gcode())
                {
                    var address = gcode.GCODE_PREFIX().GetText();
                    if (!gcodePrefixMap.ContainsKey(address))
                        gcodePrefixMap[address] = new List<double>();

                    var resolvedExpr = Visit(gcode.expr());
                    if (!resolvedExpr.IsNumeric())
                        throw new Exception($"Error: Line {gcode.expr().Start.Line}, col {gcode.expr().Start.Column}. Cannot resolve {address} param to numeric type");
                    gcodePrefixMap[address].Add((double)resolvedExpr); 
                }

                // deal with preparatory codes first
                var prepCodes = gcodePrefixMap["G"];
                //...

                // next get the feedrate
                var feedrate = gcodePrefixMap["F"];
                if (feedrate.Count > 1)
                    throw new Exception($"Error: Line {context.Start.Line}. Cannot specify multiple feedrates in a block");
                if (feedrate.Count() == 1)
                {
                    try
                    {
                        Runtime.Feedrate = (int)feedrate.First();
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Error: Line {context.Start.Line}. Could not adjust feedrate", e);
                    }
                }

                // etc. add all the other possible codes, and then call Runtime methods
            }
            else
            {
                // otherwise, it's a control flow or variable manipulation instruction.
                // delegate to other rule handlers to carry out these actions
                Visit(context);
            }

            return null;
        }

        public override object VisitIf([NotNull] FanucGCodeParser.IfContext context)
        {
            var conditionExpr = context.expr()[0];
            var thenExpr = context.expr()[1];
            var gotoExpr = context.@goto();

            bool conditional = false;
            try
            {
                var resolvedExpr = Visit(conditionExpr);
                if (resolvedExpr == null || resolvedExpr.GetType() != typeof(bool))
                    throw new Exception($"Error: Line {context.Start.Line}. Conditional expression could not be resolved to true or false");
                conditional = (bool)resolvedExpr;
            }
            catch (Exception e)
            {
                throw new Exception($"Error: Line {context.Start.Line}. Could not evaluate conditional expression", e);
            }

            // this is the "IF", intepretted
            if (conditional)
            {
                if (gotoExpr != null)
                    Visit(gotoExpr); // GOTO a block
                else
                    Visit(thenExpr); // THEN assign a var to something
                            // TODO: Could put in additional checking here to ensure that a variable was set?
            }

            return null;
        }

        // NOTE!!! this rule/node handler modifies the flow of block execution
        public override object VisitGoto([NotNull] FanucGCodeParser.GotoContext context)
        {
            uint sequenceNumber = uint.Parse(context.DIGITS().GetText());
            // set the program's current block ptr according to the sequence number
            _stack.Peek().GotoSequenceNumber(sequenceNumber);
            return null;
        }

        public override object VisitIntegerExpression([NotNull] FanucGCodeParser.IntegerExpressionContext context)
        {
            int result;
            if (!int.TryParse(context.integer().GetText(), out result))
            {
                throw new FormatException($"Error: Line {context.integer().Start.Line}, col {context.integer().Start.Column}. Could not parse integer value.");
            }
            return result;
        }

        public override object VisitRealExpression([NotNull] FanucGCodeParser.RealExpressionContext context)
        {
            double result;
            if (!double.TryParse(context.real().GetText(), out result))
            {
                throw new FormatException($"Error: Line {context.real().Start.Line}, col {context.real().Start.Column}. Could not parse double value.");
            }
            return result;
        }

        public override object VisitRelationalExpression([NotNull] FanucGCodeParser.RelationalExpressionContext context)
        {
            if (context.expr() == null || context.expr().Length != 2)
                throw new Exception("Expected left and right expressions"); // in practice this would be caught before this point

            var leftExpr = context.expr()[0];
            var rightExpr = context.expr()[1];
            var resolvedLeftExpr = Visit(leftExpr);
            if (!resolvedLeftExpr.IsNumeric())
                throw new Exception($"Error: Line {leftExpr.Start.Line}, col {leftExpr.Start.Column}. Could not resolve expression to comparable numeric type.");
            var resolvedRightExpr = Visit(rightExpr);
            if (!resolvedRightExpr.IsNumeric())
                throw new Exception($"Error: Line {rightExpr.Start.Line}, col {rightExpr.Start.Column}. Could not resolve expression to comparable numeric type.");

            bool result = false;
            double left = (double)resolvedLeftExpr;
            double right = (double)resolvedRightExpr;
            switch (context.RELATIONAL_OP().GetText())
            {
                case "EQ":
                    result = left == right;
                    break;
                case "NE":
                    result = left != right;
                    break;
                case "LT":
                    result = left < right;
                    break;
                case "GT":
                    result = left > right;
                    break;
                case "GE":
                    result = left >= right;
                    break;
                case "LE":
                    result = left <= right;
                    break;
                default:
                    throw new Exception($"Error: Line {context.Start.Line}, col {rightExpr.Start.Column}. Invalid relational operator detected.");
            }
            return result;
        }
    }
}
