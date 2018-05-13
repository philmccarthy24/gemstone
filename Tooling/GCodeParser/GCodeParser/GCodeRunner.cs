using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime;
using System.Linq.Expressions;

namespace GCodeParser
{

    public interface IMachineToolRuntime
    {
        void SetNextBlock(uint sequenceNumber);
        double? GetVariable(uint index);
        void SetVariable(uint index, double value);
    }
    
    public interface IBlock
    {
        void BlockAction(IMachineToolRuntime gcodeRuntime);
    }

    public class GotoBlock : IBlock
    {
        public uint GotoSequenceNumber { get; set; }

        public virtual void BlockAction(IMachineToolRuntime gcodeRuntime)
        {
            gcodeRuntime.SetNextBlock(GotoSequenceNumber);
        }
    }

    public class IfGotoBlock : GotoBlock
    {
        public Func<bool> ConditionalExpression;

        public override void BlockAction(IMachineToolRuntime gcodeRuntime)
        {
            if (ConditionalExpression())
                base.BlockAction(gcodeRuntime);
        }
    }

    // present just for testing and development. remove when finished.
    public class UnknownBlock : IBlock
    {
        public void BlockAction(IMachineToolRuntime gcodeRuntime)
        {
        }
    }

    public class FanucGCodeRunner : FanucGCodeParserBaseListener, IMachineToolRuntime
    {
        private IList<IBlock> _programModel;
        private uint _blockPtr;
        private IDictionary<uint, uint> _blockIdxSeqNumMap;
        private double?[] _machineVariables;

        public void RunProgram(string programContent)
        {
            _programModel = new List<IBlock>();
            _blockPtr = 0;
            _blockIdxSeqNumMap = new Dictionary<uint, uint>();
            _machineVariables = new double?[1000]; // 0 to 1000 are valid for now - allowable var ranges would be a controller specific setting

            // first, parse the program and populate the _programModel
            AntlrInputStream inputStream = new AntlrInputStream(programContent);
            FanucGCodeLexer fanucLexer = new FanucGCodeLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(fanucLexer);

            FanucGCodeParser fanucParser = new FanucGCodeParser(commonTokenStream);

            // Parse GCode - TODO detect parse errors and throw
            var progContext = fanucParser.program();
            
            // TODO: walk tree with this as a listener, perform actions for encountered blocks

            // now the program is loaded, execute it
            do
            {
                var block = _programModel[(int)_blockPtr];
                uint currBlockIdx = _blockPtr;
                block.BlockAction(this);
                if (currBlockIdx == _blockPtr)
                    _blockPtr++; // if the block's side effects didn't include changing the block ptr, move to next block
            } while (_blockPtr < _programModel.Count);
        }

        public override void ExitBlock([NotNull] FanucGCodeParser.BlockContext context)
        {
            //if (context.sequenceNumber() != null)
            base.ExitBlock(context);
        }

        ///////////////////////////////////////
        // IGCodeRuntime implementation
        //
        public void SetNextBlock(uint sequenceNumber)
        {
            _blockPtr = _blockIdxSeqNumMap[sequenceNumber];
        }

        public double? GetVariable(uint index)
        {
            return _machineVariables[index];
        }

        public void SetVariable(uint index, double value)
        {
            if (index >= _machineVariables.Length)
                throw new Exception("Error: Attempt to set a variable outside supported limits");

            _machineVariables[index] = value;
        }
    }

    /*class FanucGCodeModel
    {
        public Dictionary<string, Expression> 
        // was worried about how to get context for gcode bocks, but I think we can do this in the VisitBlock method.
        // ...
    }*/

    class FanucGCodeExpressionTreeBuilder : FanucGCodeParserBaseVisitor<Expression>
    {
        public IMachineToolRuntime Runtime { get; set; }

        // Visitor entrypoint node handler. Assembles a BlockExpression from the list of gcode blocks (note
        // the term block is being used in two ways here!)
        public override Expression VisitProgram([NotNull] FanucGCodeParser.ProgramContext context)
        {
            BlockExpression program = Expression.Block(context.programContent().block().Select(bc => VisitBlock(bc)).Where(e => e != null));
            return program;
        }

        // Visit a gcode block. If there's a sequence number, turn it into a label. Then visit the gcode
        // block's child AST nodes. return as an Expression
        public override Expression VisitBlock([NotNull] FanucGCodeParser.BlockContext context)
        {
            var statement = context.blockContent().statement();
            if (statement != null)
            {
                // we are dealing with a statement - is it a set of gcode blocks? if so, work out what the IMachineToolRuntime command should be, and
                // return an expression that invokes a call on that object.

                // otherwise, just return the expression returned by visiting the AST childern
                // else {
                    var blockExpr = Visit(context);
                //}
            }

            var seqNum = context.sequenceNumber();
            if (seqNum != null)
            {
                var lt = Expression.Label(seqNum.INTEGER().GetText());
                blockExpr = Expression.Block(Expression.Label(lt), blockExpr); // do we need to check that blockExpr isn't null?
            }

            // TODO: handle the case where just a comment is specified without statement, expression or sequence number.
            // we should return null, so the block is ignored (filtered out in the VisitProgram method above)

            return blockExpr;
        }

        public override Expression VisitGoto([NotNull] FanucGCodeParser.GotoContext context)
        {
            return base.VisitGoto(context);
        }

        MethodCallExpression GetCallExpression<T>(Expression<Func<T>> e)
        {
            return e.Body as MethodCallExpression;
        }

    }
}
