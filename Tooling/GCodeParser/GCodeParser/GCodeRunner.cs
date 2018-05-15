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

        private uint _lastBlockIdx = 0;

        public override void ExitBlock([NotNull] FanucGCodeParser.BlockContext context)
        {
            if (context.sequenceNumber() != null)
                _SeqNumToBlockIdxMap[uint.Parse(context.sequenceNumber().INTEGER().GetText())] = _lastBlockIdx++;
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

    // the interpreter 
    internal sealed class SubExprEvaluation
    {
        T EvaluateAs<T>()
        {
            return (T)EvalResult;
        }

        public object EvalResult { get; set;}
        
        public Stack<string> ParseErrors { get; set; } = new Stack<string>();
    }

    class FanucGCodeInterpreter : FanucGCodeParserBaseVisitor<SubExprEvaluation>
    {
        private IMachineToolRuntime _runtime;
        private GCodeJumpTable _gcodeJumpTable = new GCodeJumpTable(); // this might need to be in a stack, depending on how we handle G65s

        public FanucGCodeInterpreter(IMachineToolRuntime runtime)
        {
            _runtime = runtime;
        }

        // Visitor entrypoint node handler. Builds a table of sequence number to block indeces, for correct execution
        // of GOTOs, then runs the program evaluating each block at a time
        public override SubExprEvaluation VisitProgram([NotNull] FanucGCodeParser.ProgramContext context)
        {
            // Generate the block number jump table
            ParseTreeWalker treeWalker = new ParseTreeWalker();
            treeWalker.Walk(_gcodeJumpTable, context);

            foreach (var blockContext in context.programContent().block())
            {
                VisitBlock(blockContext);
            }

            return null;
        }

        // Visit a gcode block. If there's a sequence number, turn it into a label. Then visit the gcode
        // block's child AST nodes. return as an Expression
        public override SubExprEvaluation VisitBlock([NotNull] FanucGCodeParser.BlockContext context)
        {
            var statement = context.blockContent().statement();
            if (statement != null)
            {
                // we are dealing with a statement - is it a set of gcode blocks? if so, work out what the IMachineToolRuntime command should be, and execute it.
                // resolve sub-expressions to doubles or integers. error out if they can't be resolved to these.
                if (statement.gcode() != null)
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
                        // I'm not sure how to put this into an expression tree to set a property on an already-instantiated class...

                        var assigner = GetAssigner<IMachineToolRuntime, double>(u => u.Feedrate);
                        //assigner.Compile(_runtime, addresses["F"]);
                        // this is't right


                        /*var parameter = Expression.Parameter(typeof(Person), "x");
                        var member = Expression.Property(parameter, "Id"); //x.Id
                        var constant = Expression.Constant(3);
                        var body = Expression.GreaterThanOrEqual(member, constant); //x.Id >= 3
                        var finalExpression = Expression.Lambda<Func<Person, bool>>(body, param); //x => x.Id >= 3
                        */

            /*
            var feedratePropSetterExpr = GetPropertySetter<IMachineToolRuntime, double>(_runtime, "Feedrate");
            blockExpr = Expression.Block(
                feedratePropSetterExpr
                );
            blockExpr = GetCallExpression(e => _runtime.Feedrate)
            addresses["F"];
            */ /*
        }
    }
}
else
{
    // otherwise, it's control flow or variable manipulation instructions.
    // just return the drt Expression returned by visiting the AST childern
    blockExpr = Visit(context);
}

var seqNum = context.sequenceNumber();
if (seqNum != null)
{
    var lt = Expression.Label(seqNum.INTEGER().GetText());
    blockExpr = Expression.Block(Expression.Label(lt), blockExpr); // do we need to check that blockExpr isn't null?
}

// TODO: handle the case where just a comment is specified without statement, expression or sequence number.
// we should return null, so the block is ignored (filtered out in the VisitProgram method above)

return blockExpr;*/
            return null;
        }

        public override Expression VisitGoto([NotNull] FanucGCodeParser.GotoContext context)
        {
            return base.VisitGoto(context);
        }

        private static MethodCallExpression GetCallExpression<T>(Expression<Func<T>> e)
        {
            return e.Body as MethodCallExpression;
        }

        /*
        static LambdaExpression GetPropertySetter<TElement, TValue>(TElement elem, string propertyName)
        {
            Type elementType = elem.GetType();

            PropertyInfo pi = elementType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            MethodInfo mi = pi.GetSetMethod();  //  This retrieves the 'get_LastName' method

            ParameterExpression oParam = Expression.Parameter(elementType, "obj");
            ParameterExpression vParam = Expression.Parameter(typeof(TValue), "val");
            MethodCallExpression mce = Expression.Call(oParam, mi, vParam);
            return Expression.Lambda<Action<TElement, TValue>>(mce, oParam, vParam);
        }
        */

        private static Expression<Action<TClass, TValue>> GetAssigner<TClass, TValue>(Expression<Func<TClass, TValue>> propertyAccessor)
        {
            var prop = ((MemberExpression)propertyAccessor.Body).Member;
            var typeParam = Expression.Parameter(typeof(TClass));
            var valueParam = Expression.Parameter(typeof(TValue));
            return Expression.Lambda<Action<TClass, TValue>>(
                Expression.Assign(
                    Expression.MakeMemberAccess(typeParam, prop),
                    valueParam), typeParam, valueParam);

        }

    }
}
