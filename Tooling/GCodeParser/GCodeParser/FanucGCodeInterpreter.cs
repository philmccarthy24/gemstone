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
using GCode.Utility;

namespace GCode.Interpreter
{

    ///////////////////////////////////////////////////
    // Interfaces - should probably be moved somewhere else, non-Fanuc specific

    public interface IMachineToolRuntime
    {
        double Feedrate { get; set; }

        // TODO: add functions to drive simulated machine tool here
    }

    public interface IMachineVariableTable
    {
        double? this[uint idx] { get; set; }
    }

    public interface IGCodeInterpreter : IMachineVariableTable
    {
        // TODO: add some way of querying local and common variables here

        IMachineToolRuntime Runtime { get; set; }

        void RunProgram(IGCodeProgram startProgram);
    }

    //////////////////////////////////////////////////////////

    public class FanucMachineToolRuntime : IMachineToolRuntime
    {
        public double Feedrate { get; set; }
    }

    public class FanucGCodeInterpreter : FanucGCodeParserBaseVisitor<object>, IGCodeInterpreter
    {
        private Stack<IGCodeProgram> _stack;
        private double?[] _commonVariables = new double?[600]; // #100 to #199 map to 0-99. #500-#999 map to 100-599

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
                            throw new GCodeException($"Cannot resolve {address} param to numeric type", _stack.Peek().Name, gcode.expr().Start.Line, gcode.expr().Start.Column);
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
                            throw new GCodeException("Cannot specify multiple feedrates in a block", _stack.Peek().Name, context.Start.Line);
                        if (feedrate.Count() == 1)
                        {
                            try
                            {
                                Runtime.Feedrate = (int)feedrate.First();
                            }
                            catch (Exception e)
                            {
                                throw new GCodeException("Could not adjust feedrate", _stack.Peek().Name, context.Start.Line, e);
                            }
                        }
                    }

                    // etc. add all the other possible codes, and then call Runtime methods
                }
                else
                {
                    // it's a program flow control statement (IF, GOTO etc).
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
            else throw new GCodeException("Unexpected content encountered.", _stack.Peek().Name, context.Start.Line);

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
                    throw new GCodeException("Conditional expression could not be resolved to true or false", _stack.Peek().Name, context.Start.Line);
                conditional = (bool)resolvedExpr;
            }
            catch (Exception e)
            {
                throw new GCodeException("Could not evaluate conditional expression", _stack.Peek().Name, context.Start.Line, e);
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
                throw new GCodeException("Could not parse integer value.", _stack.Peek().Name, context.integer().Start.Line, context.integer().Start.Column);
            }
            return result;
        }

        public override object VisitRealExpression([NotNull] FanucGCodeParser.RealExpressionContext context)
        {
            double result;
            if (!double.TryParse(context.real().GetText(), out result))
            {
                throw new GCodeException("Could not parse double value.", _stack.Peek().Name, context.real().Start.Line, context.real().Start.Column);
            }
            return result;
        }

        private NumericOperands EvaluateNumericBinaryOperands(FanucGCodeParser.ExprContext[] operands)
        {
            if (operands == null || operands.Length != 2)
                throw new InvalidOperationException("Expected left and right expressions"); // in practice this would be caught before this point

            double? evaluatedLeft;
            var leftExpr = operands[0];
            var resolvedLeftExpr = Visit(leftExpr);
            if (!resolvedLeftExpr.IsNumeric())
                throw new GCodeException("Could not resolve operand to comparable numeric type", _stack.Peek().Name, leftExpr.Start.Line, leftExpr.Start.Column);
            try
            {
                evaluatedLeft = resolvedLeftExpr.NormaliseNumeric();
            }
            catch (Exception e)
            {
                // re-throw exception, giving it GCode Line/col context
                throw new GCodeException(e.Message, _stack.Peek().Name, leftExpr.Start.Line, leftExpr.Start.Column);
            }

            double? evaluatedRight;
            var rightExpr = operands[1];
            var resolvedRightExpr = Visit(rightExpr);
            if (!resolvedRightExpr.IsNumeric())
                throw new GCodeException("Could not resolve operand to comparable numeric type.", _stack.Peek().Name, rightExpr.Start.Line, rightExpr.Start.Column);
            try
            {
                evaluatedRight = resolvedRightExpr.NormaliseNumeric();
            }
            catch (Exception e)
            {
                // re-throw exception, giving it GCode Line/col context
                throw new GCodeException(e.Message, _stack.Peek().Name, rightExpr.Start.Line, rightExpr.Start.Column);
            }
            return new NumericOperands { Left = evaluatedLeft, Right = evaluatedRight };
        }

        public override object VisitRelationalExpression([NotNull] FanucGCodeParser.RelationalExpressionContext context)
        {
            bool result = false;
            var operands = EvaluateNumericBinaryOperands(context.expr());

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
                    throw new GCodeException("Invalid relational operator detected.", _stack.Peek().Name, context.RELATIONAL_OP().Symbol.Line, context.RELATIONAL_OP().Symbol.Column);
            }
            return result;
        }

        // note this will collapse MachineVariable objects to double?s.
        public override object VisitArithmeticExpression([NotNull] FanucGCodeParser.ArithmeticExpressionContext context)
        {
            double? result = null;
            var operands = EvaluateNumericBinaryOperands(context.expr());

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
                else throw new GCodeException("Unrecognised arithmetic expression type", _stack.Peek().Name, context.Start.Line);
            }
            return result;
        }

        public override object VisitAssignmentExpression([NotNull] FanucGCodeParser.AssignmentExpressionContext context)
        {
            // p383 of the manual specifies that if #0 is assigned to a variable, either directly or via another variable (set to #0), 
            // the variable is set to #0.
            // however if #0 (null) is the result of arithmetic, the value to assign will be 0.

            // So we need to check that rhs directly resolves to a MachineVariable, and if mv.Value == null, then we set lhs to null.
            // otherwise if rhs is a double?, it means calculations have been done. Therefore if nullDbl == null, we set lhs to 0.0.
            //.... worth some more thought and experimentation. how will resolving var reference expressions work etc

            // evaluate the right hand side
            var rhs = context.expr()[1];
            var resolvedRhs = Visit(rhs);
            double? evaluatedRhs;
            try
            {
                if (resolvedRhs.GetType() == typeof(MachineVariable))
                {
                    evaluatedRhs = ((MachineVariable)resolvedRhs).Value; // NOTE this can evaluate to null, which is Ok
                }
                else if (resolvedRhs.IsNumeric())
                {
                    evaluatedRhs = resolvedRhs.NormaliseNumeric() ?? 0; // Must never evaluate to null
                }
                else throw new GCodeException("Assignment argument must be a machine variable or numeric", _stack.Peek().Name, rhs.Start.Line, rhs.Start.Column);
            }
            catch (GCodeException)
            {
                throw;
            }
            catch (Exception e)
            {
                // re-throw the exception, giving it GCode line/col context, for the rhs not being valid
                throw new GCodeException(e.Message, _stack.Peek().Name, rhs.Start.Line, rhs.Start.Column);
            }

            // evaluate left hand side, and do the assignment
            var lhs = context.expr()[0];
            var resolvedLhs = Visit(lhs) as MachineVariable;
            if (resolvedLhs == null)
                throw new GCodeException("Target of assignment must be a machine variable", _stack.Peek().Name, lhs.Start.Line, lhs.Start.Column);
            try
            {
                resolvedLhs.Value = evaluatedRhs;
            }
            catch (Exception e)
            {
                // re-throw the exception, giving it GCode line/col context
                throw new GCodeException(e.Message, _stack.Peek().Name, lhs.Start.Line, lhs.Start.Column);
            }
            
            return null;
        }

        public override object VisitVariableExpression([NotNull] FanucGCodeParser.VariableExpressionContext context)
        {
            MachineVariable ncVar;
            var varExpr = context.variable().expr();
            var systemVar = context.variable().SYSTEMVAR_CONST_OR_COMMONVAR_IDENTIFIER();

            if (systemVar != null)
                throw new NotImplementedException("Not there yet");
            else
            {
                if (varExpr != null)
                {
                    // this is a variable of the form #[expr]
                    var resolvedVarExpr = Visit(varExpr);
                    if (!resolvedVarExpr.IsNumeric())
                        throw new GCodeException("Could not resolve variable lookup expression to numeric type.", _stack.Peek().Name, varExpr.Start.Line, varExpr.Start.Column);
                    var evaluatedVarExpr = resolvedVarExpr.NormaliseNumeric() ?? 0;
                    if (evaluatedVarExpr < 0)
                        throw new GCodeException("Variable lookup expression must be non-negative", _stack.Peek().Name, varExpr.Start.Line, varExpr.Start.Column);
                    ncVar = new MachineVariable(this, (uint)evaluatedVarExpr);
                }
                else
                {
                    // this is the simplest var expression: #n where N is an int
                    ncVar = new MachineVariable(this, uint.Parse(context.variable().DIGITS().GetText()));
                }
            }
            return ncVar;
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
                else if (idx >= 100 && idx <= 199)
                    var = _commonVariables[idx - 100];
                else if (idx >= 500 && idx <= 999)
                    var = _commonVariables[idx - 400];
                /*
                 * add special variables here, eg to get current machine co-ordinates etc
                 */ 
                else throw new InvalidOperationException($"Bad variable number {idx}");
                return var;
            }

            set
            {
                if (idx == 0)
                    throw new InvalidOperationException("Cannot assign to variable #0");

                if (idx <= NUM_LOCALS)
                    _stack.Peek().LocalVariables[idx - 1] = value;
                else if (idx >= 100 && idx <= 199)
                    _commonVariables[idx - 100] = value;
                else if (idx >= 500 && idx <= 999)
                    _commonVariables[idx - 400] = value;
                else throw new InvalidOperationException($"Bad variable number {idx}");
            }
        }
    }
}
