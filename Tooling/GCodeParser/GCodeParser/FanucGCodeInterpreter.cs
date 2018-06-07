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
using System.Diagnostics;

namespace GCodeParser
{

    public interface IMachineToolRuntime
    {
        double Feedrate { get; set; }

        // TODO: add functions to drive simulated machine tool here
    }

    public interface IMachineVariableTable
    {
        double? this[uint idx] { get;  set; }
    }

    public interface IGCodeInterpreter : IMachineVariableTable
    {
        // TODO: add some way of querying local and common variables here

        IMachineToolRuntime Runtime { get; set; }

        void RunProgram(IGCodeProgram startProgram);
    }

    public class FanucMachineToolRuntime : IMachineToolRuntime
    {
        public double Feedrate { get; set; }
    }

    internal static class ObjectExtenions
    {
        public static bool IsNumeric(this object objToTest)
        {
            return (objToTest.GetType() == typeof(int) || objToTest.GetType() == typeof(double) || objToTest.GetType() == typeof(MachineVariable));
        }
    }

    internal sealed class NumericOperands
    {
        public double? Left { get; set; }
        public double? Right { get; set; }
    }

    // class to represent machine variables during interpretation
    internal sealed class MachineVariable
    {
        private IMachineVariableTable _varTable;
        private uint _varIdx;

        public MachineVariable(IMachineVariableTable varTable, uint varIdx)
        {
            _varTable = varTable;
            _varIdx = varIdx;
        }

        public double? Value
        {
            get
            {
                return _varTable[_varIdx];
            }
            set
            {
                _varTable[_varIdx] = value;
            }
        }

        public static implicit operator double?(MachineVariable mv)
        {
            return mv.Value;
        }
    }

    public class FanucGCodeInterpreter : FanucGCodeParserBaseVisitor<object>, IGCodeInterpreter
    {
        private Stack<IGCodeProgram> _stack;
        private double?[] _commonVariables = new double?[500];              // TODO: add support for #100 to #199 (volative macros) as well.

        private const uint NUM_LOCALS = 33;

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
            if (context.blockContent() == null)
                return null; // empty block (possibly with sequence number)

            var statement = context.blockContent().statement();
            var expr = context.blockContent().expr();
            var comment = context.blockContent().comment();

            // are we dealing with a statement, with a set of gcode addresses and parameter? if so, work out what the IMachineToolRuntime command should be, and execute it.
            // resolve sub-expressions to doubles or integers. error out if they can't be resolved to these.
            if (statement != null)
            {
                var gcodeStatement = statement.gcode();
                if (gcodeStatement.Length > 0)
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
                    if (gcodePrefixMap.ContainsKey("G"))
                    {
                        var prepCodes = gcodePrefixMap["G"];
                        //...
                    }

                    // next get the feedrate
                    if (gcodePrefixMap.ContainsKey("F"))
                    {
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
                    }

                    // etc. add all the other possible codes, and then call Runtime methods
                }
                else
                {
                    // otherwise, it's a control flow instruction.
                    // delegate to other rule handlers to carry out these actions
                    Visit(statement);
                }
            }
            else if (expr != null)
            {
                // we have an expression (variable manipulation etc)
                // delegate to other rule handlers to carry out these actions
                Visit(expr);
            }
            else if (comment != null)
            {
                // we're dealing with a comment. TRACE it out for a giggle.
                Debug.WriteLine(comment.CTRL_OUT_TEXT().GetText());
            }
            else throw new Exception($"Error: Line {context.Start.Line}. Unexpected content encountered.");

            return null;
        }

        public override object VisitIf([NotNull] FanucGCodeParser.IfContext context)
        {
            var conditionExpr = context.expr()[0];
            var thenExpr = context.expr().Length > 1 ? context.expr()[1] : null;
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

        private NumericOperands ResolveNumericBinaryOperands(FanucGCodeParser.ExprContext[] operands)
        {
            if (operands == null || operands.Length != 2)
                throw new Exception("Expected left and right expressions"); // in practice this would be caught before this point

            var leftExpr = operands[0];
            var rightExpr = operands[1];
            var resolvedLeftExpr = Visit(leftExpr);
            if (!resolvedLeftExpr.IsNumeric())
                throw new Exception($"Error: Line {leftExpr.Start.Line}, col {leftExpr.Start.Column}. Could not resolve operand to comparable numeric type.");
            var resolvedRightExpr = Visit(rightExpr);
            if (!resolvedRightExpr.IsNumeric())
                throw new Exception($"Error: Line {rightExpr.Start.Line}, col {rightExpr.Start.Column}. Could not resolve operand to comparable numeric type.");

            return new NumericOperands { Left = (double?)resolvedLeftExpr, Right = (double?)resolvedRightExpr };
        }

        public override object VisitRelationalExpression([NotNull] FanucGCodeParser.RelationalExpressionContext context)
        {
            bool result = false;
            var operands = ResolveNumericBinaryOperands(context.expr());

            // for Fanuc, the logic for deciding how comparison for undefined variables is handled is specified in the manual p383 para (c).
            // in C#, comparing null with anything non-null always evaluates to false, apart from (null == null) == true and (null != anything) == true.
            switch (context.RELATIONAL_OP().GetText())
            {
                case "EQ":
                    result = operands.Left == operands.Right;
                    break;
                case "NE":
                    result = operands.Left != operands.Right;
                    break;
                case "LT":
                    result = operands.Left < operands.Right;
                    break;
                case "GT":
                    result = operands.Left > operands.Right;
                    break;
                case "GE":
                    if (operands.Left == null && operands.Right == null)
                        result = true;
                    else
                        result = operands.Left >= operands.Right;
                    break;
                case "LE":
                    if (operands.Left == null && operands.Right == null)
                        result = true;
                    else
                        result = operands.Left <= operands.Right;
                    break;
                default:
                    throw new Exception($"Error: Line {context.RELATIONAL_OP().Symbol.Line}, col {context.RELATIONAL_OP().Symbol.Column}. Invalid relational operator detected.");
            }
            return result;
        }

        // note this will collapse MachineVariable objects to double?s.
        public override object VisitArithmeticExpression([NotNull] FanucGCodeParser.ArithmeticExpressionContext context)
        {
            double? result = null;
            var operands = ResolveNumericBinaryOperands(context.expr());

            if (operands.Left != null && operands.Right != null)
            {
                // for Fanuc, the logic for deciding how arithmetic for undefined variables is handled is specified in the manual p383 para (b)
                if (context.PLUS() != null)
                    result = operands.Left + operands.Right;
                else if (context.MINUS() != null)
                    result = operands.Left - operands.Right;
                else if (context.MULTIPLY() != null)
                    result = operands.Left * operands.Right;
                else if (context.DIVIDE() != null)
                    result = operands.Left / operands.Right;
                else if (context.MOD() != null)
                    result = operands.Left % operands.Right;
                else throw new Exception($"Error: Line {context.Start.Line}. Unrecognised arithmetic expression type");
            }
            return result;
        }

        public override object VisitAssignmentExpression([NotNull] FanucGCodeParser.AssignmentExpressionContext context)
        {
            // p383 of the manual specifies that if #0 is assigned to a variable, either directly or via another variable (set to #0), the variable is set to #0.
            // however if #0 (null) is the result of arithmetic, the value to assign will be 0. This is a nasty gotcha!
            
            // So we need to check that rhs directly resolves to a MachineVariable, and if mv.Value == null, then we set lhs to null.
            // otherwise if rhs is a double?, it means calculations have been done. Therefore if nullDbl == null, we set lhs to 0.0.
            //.... worth some more thought and experimentation. how will resolving var reference expressions work etc

            var lhs = context.expr()[0];
            var rhs = context.expr()[1];
            var resolvedLhs = Visit(lhs);
            if (resolvedLhs)

            return base.VisitAssignmentExpression(context);
        }

        public override object VisitVariableExpression([NotNull] FanucGCodeParser.VariableExpressionContext context)
        {
            // return a MachineVariable object here
            return base.VisitVariableExpression(context);
        }

        // IMachineVariableTable implementation that allows lookup / setting of machine vars
        public double? this[uint idx]
        {
            get
            {
                double? var = null;
                if (idx == 0)
                    var = null;
                else if (idx > 0 && idx <= NUM_LOCALS)
                    var = _stack.Peek().LocalVariables[idx - 1];
                else if (idx >= 500 && idx <= 999)
                    var = _commonVariables[idx - 500];
                /*
                 * add special variables here, eg to get current machine co-ordinates etc
                 */ 
                else throw new Exception("Bad variable number");
                return var;
            }

            set
            {

                if (idx == 0)
                    throw new Exception("Error: Cannot set #0");

                if (idx <= NUM_LOCALS)
                    _stack.Peek().LocalVariables[idx - 1] = value;
                else if (idx >= 500 && idx <= 999)
                    _commonVariables[idx - 500] = value;
                else throw new Exception("Error: Attempt to set a variable outside supported limits");
            }
        }
    }
}
