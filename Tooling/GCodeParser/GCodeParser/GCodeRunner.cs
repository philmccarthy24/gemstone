using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime;

namespace GCodeParser
{

    public interface IGCodeRuntime
    {
        void SetNextBlock(uint sequenceNumber);
        double? GetVariable(uint index);
        void SetVariable(uint index, double value);
    }
    
    public interface IBlock
    {
        void BlockAction(IGCodeRuntime gcodeRuntime);
    }

    public class GotoBlock : IBlock
    {
        public uint GotoSequenceNumber { get; set; }

        public virtual void BlockAction(IGCodeRuntime gcodeRuntime)
        {
            gcodeRuntime.SetNextBlock(GotoSequenceNumber);
        }
    }

    public class IfGotoBlock : GotoBlock
    {
        public Func<bool> ConditionalExpression;

        public override void BlockAction(IGCodeRuntime gcodeRuntime)
        {
            if (ConditionalExpression())
                base.BlockAction(gcodeRuntime);
        }
    }

    // present just for testing and development. remove when finished.
    public class UnknownBlock : IBlock
    {
        public void BlockAction(IGCodeRuntime gcodeRuntime)
        {
        }
    }

    public class FanucGCodeRunner : FanucGCodeParserBaseListener, IGCodeRuntime
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
}
