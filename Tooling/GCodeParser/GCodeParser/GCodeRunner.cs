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
        double? GetVariable(uint index);
        void SetVariable(uint index, double value);

        void PushLocals();  // Is the untime the appropriate place to have a stack? Isn't this the domain of the interpreter? ...
        void PopLocals();

        double Feedrate { get; set; }
    }

    public class FanucMachineToolRuntime : IMachineToolRuntime
    {
        // #1 to #33 local vars, in a stack (so idx 0 contains #1)
        private Stack<double?[]> _localsStack = new Stack<double?[]>();
        private const uint NUM_LOCALS = 33;

        // idx 0 of this array is #34 of the machine vars
        // we could apply additional mapping here, as the full 34-999 range isn't usually accessible to users
        private double?[] _commonVariables = new double?[1000 - NUM_LOCALS];

        public FanucMachineToolRuntime()
        {
            // locals is empty, so push a new array
            PushLocals();
        }

        public double Feedrate { get; set; }

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

        public void PushLocals()
        {
            _localsStack.Push(new double?[NUM_LOCALS]);
        }

        public void PopLocals()
        {
            if (_localsStack.Count == 1)
                throw new Exception("Cannot pop locals past top of stack");
            _localsStack.Pop();
        }
    }


    public class FanucGCodeRunner
    {
        private IMachineToolRuntime _machineToolRuntime;
        
        public FanucGCodeRunner(IMachineToolRuntime runtime)
        {
            _machineToolRuntime = runtime;
        } 

        public void RunProgram(string programContent)
        {
            // parse the program
            AntlrInputStream inputStream = new AntlrInputStream(programContent);
            FanucGCodeLexer fanucLexer = new FanucGCodeLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(fanucLexer);

            FanucGCodeParser fanucParser = new FanucGCodeParser(commonTokenStream);

            // Parse GCode - TODO detect parse errors and throw
            var progContext = fanucParser.program();

            // translate the AST to .NET DRT Expression Tree, which can be executed
            var interpreter = new FanucGCodeInterpreter(_machineToolRuntime);
            interpreter.Visit(progContext);
        }
    }

    public class GCodeJumpTable : FanucGCodeParserBaseListener
    {
        private IDictionary<uint, uint> _SeqNumToBlockIdxMap = new Dictionary<uint, uint>();

        private uint _currentBlockIdx = 0;

        public override void ExitBlock([NotNull] FanucGCodeParser.BlockContext context)
        {
            if (context.sequenceNumber() != null)
                _SeqNumToBlockIdxMap[uint.Parse(context.sequenceNumber().INTEGER().GetText())] = _currentBlockIdx;

            _currentBlockIdx++;
            base.ExitBlock(context);
        }

        public uint this[uint sequenceNumber]
        {
            get
            {
                return _SeqNumToBlockIdxMap[sequenceNumber];
            }
        }
    }

    // a convenience wrapper around an object, with an error stack trace for managing parse errors.
    // explicit conversion operators used to enforce parse rule expectations
    // TODO: might be useful to have these casts throw an exception type with a stack trace, identifying
    // line and char num it went wrong?
    internal sealed class SubExprEvaluation
    {
        private object _evalResult;
        public SubExprEvaluation(object result)
        {
            _evalResult = result;
        }

        public static explicit operator double(SubExprEvaluation exprEval)
        {
            if (exprEval._evalResult == null)
                throw new Exception("Bad evaluation result: expression was not evaluated. Check errors");

            if (exprEval._evalResult.GetType() != typeof(double) && exprEval._evalResult.GetType() != typeof(int))
                throw new Exception("Bad evaluation result: not numeric");

            return (double)exprEval._evalResult;  // explicit conversion
        }

        public static explicit operator int(SubExprEvaluation exprEval)
        {
            int result;
            if (exprEval._evalResult == null)
                throw new Exception("Bad evaluation result: expression was not evaluated. Check errors");

            if (exprEval._evalResult.GetType() != typeof(double) && exprEval._evalResult.GetType() != typeof(int))
                throw new Exception("Bad evaluation result: not a numeric type");

            if (exprEval._evalResult.GetType() == typeof(double))
            {
                result = Convert.ToInt32(exprEval._evalResult);
                if (result != (double)exprEval._evalResult)
                    throw new Exception("Error converting double to int: loss of precision would occur");
            }
            else result = (int)exprEval._evalResult;

            return result;  // explicit conversion
        }

        // relational expressions should evaluate to bool
        public static explicit operator bool (SubExprEvaluation exprEval)
        {
            if (exprEval._evalResult == null)
                throw new Exception("Bad evaluation result: expression was not evaluated. Check errors");
            if (exprEval._evalResult.GetType() != typeof(bool))
                throw new Exception("Bad evaluation result: not boolean");

            return (bool)exprEval._evalResult;  // explicit conversion
        }


        public Stack<string> ParseErrors { get; set; } = new Stack<string>();
    }

    class FanucGCodeInterpreter : FanucGCodeParserBaseVisitor<SubExprEvaluation>
    {
        private IMachineToolRuntime _runtime;
        private GCodeJumpTable _gcodeJumpTable = new GCodeJumpTable(); // this might need to be in a stack, depending on how we handle G65s
        private uint _nextBlockPtr = 0; // this might need to be in a stack, depending on how we handle G65s
        // TODO: I think possibly that variable handling belongs in the interpreter, not runtime - simply because we will need to handle stacks for G65s, and it's
        // sensible to do a Pop/Push for this related data in one place. Noting that GUIs etc might need to interrogate the current macro variables

        public FanucGCodeInterpreter(IMachineToolRuntime runtime)
        {
            _runtime = runtime;
        }

        // Visitor top level program rule/node handler. Builds a table of sequence number to block indeces, for correct execution
        // of GOTOs, then runs the program evaluating each block at a time
        public override SubExprEvaluation VisitProgram([NotNull] FanucGCodeParser.ProgramContext context)
        {
            // Generate the block number jump table
            ParseTreeWalker treeWalker = new ParseTreeWalker();
            treeWalker.Walk(_gcodeJumpTable, context);

            // now interpret the blocks of the gcode program
            var blockContexts = context.programContent().block();
            do
            {
                uint currBlockPtr = _nextBlockPtr;
                VisitBlock(blockContexts[_nextBlockPtr]);
                if (currBlockPtr == _nextBlockPtr)
                    // ptr was unmodified by block, so go to next block
                    _nextBlockPtr++;
            } while (_nextBlockPtr < blockContexts.Length);

            return null;
        }

        // Visit a gcode block
        public override SubExprEvaluation VisitBlock([NotNull] FanucGCodeParser.BlockContext context)
        {
            var statement = context.blockContent().statement();
            
            // are we dealing with a statement, with a set of gcode addresses and parameter? if so, work out what the IMachineToolRuntime command should be, and execute it.
            // resolve sub-expressions to doubles or integers. error out if they can't be resolved to these.
            if (statement != null && statement.gcode() != null)
            {
                var addresses = new Dictionary<string, SubExprEvaluation>();
                var gcodeContexts = statement.gcode();
                foreach (var gcodeContext in gcodeContexts)
                {
                    addresses[gcodeContext.GCODE_PREFIX().GetText()] = Visit(gcodeContext.expr());
                }

                // for now, just support setting the feedrate
                if (addresses.ContainsKey("F"))
                {
                    try
                    {
                        _runtime.Feedrate = (int)addresses["F"];
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Error: Line {context.Start.Line}. Feedrate could not be set", e);
                    }
                        
                }
            }
            else
            {
                // otherwise, it's a control flow or variable manipulation instruction.
                // delegate to other rule handlers to carry out these actions
                Visit(context);
            }

            return null;
        }

        public override SubExprEvaluation VisitIf([NotNull] FanucGCodeParser.IfContext context)
        {
            return base.VisitIf(context);
        }

        // NOTE!!! this rule/node handler modifies the flow of block execution via the _nextBlockPtr!
        public override SubExprEvaluation VisitGoto([NotNull] FanucGCodeParser.GotoContext context)
        {
            uint sequenceNumber = uint.Parse(context.INTEGER().GetText());
            // lookup block index for sequence number
            var nextBlockIndex = _gcodeJumpTable[sequenceNumber];
            _nextBlockPtr = nextBlockIndex;
            return null;
        }
    }
}
