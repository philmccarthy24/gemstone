using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using GCode.Utility;
using GCodeParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCode.Interpreter
{
    public interface IGCodeProgram
    {
        double?[] LocalVariables { get; set; }
        string Name { get; set; }
        string Content { get; set; }
        uint CurrentBlockIndex { get; set; }
        ParserRuleContext RunContext { get; }

        void GotoSequenceNumber(uint sequenceNumber);
    }

    public class FanucGCodeProgram : FanucGCodeParserBaseListener, IGCodeProgram, IAntlrErrorListener<IToken>
    {
        ////////////////////////////////////////////////////////////////////////
        // IGCodeProgram implementation

        private IDictionary<uint, uint> _seqNumToBlockIdxMap;

        private const uint NUM_LOCALS = 33;
        // idx 0 = #1 etc
        public double?[] LocalVariables { get; set; } 

        private string _name = null;
        // Setting this property triggers loading the program from disk.
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name != null)
                    throw new Exception("GCode program object previously loaded");

                Content = File.ReadAllText($"{value}.prg"); // TODO: work out path handling
            }
        }

        private string _content = null;
        // Setting this property triggers parsing the gcode and building the
        // sequence number jump table
        public string Content
        {
            get
            {
                return _content;
            }

            set
            {
                // initialise object state
                _seqNumToBlockIdxMap = new Dictionary<uint, uint>();
                LocalVariables = new double?[NUM_LOCALS];
                CurrentBlockIndex = 0;
                _tempBlockNum = 0;
                _parseErrors = new List<GCodeException>();

                // parse the program
                AntlrInputStream inputStream = new AntlrInputStream(value);
                FanucGCodeLexer fanucLexer = new FanucGCodeLexer(inputStream);
                CommonTokenStream commonTokenStream = new CommonTokenStream(fanucLexer);

                FanucGCodeParser fanucParser = new FanucGCodeParser(commonTokenStream);

                fanucParser.RemoveErrorListeners();
                fanucParser.AddErrorListener(this);

                // Parse GCode
                RunContext = fanucParser.program();

                // if we have any parse errors, throw
                if (_parseErrors.Count == 1)
                    throw _parseErrors[0];
                if (_parseErrors.Count > 1)
                    throw new AggregateException(_parseErrors);

                // Generate the block number jump table (as well as set the
                // program name - underlying var)
                ParseTreeWalker treeWalker = new ParseTreeWalker();
                treeWalker.Walk(this, RunContext);
            }
        }

        public uint CurrentBlockIndex { get; set; }

        public ParserRuleContext RunContext { get; private set; }

        // This sets the current block index from the given sequence number
        public void GotoSequenceNumber(uint sequenceNumber)
        {
            CurrentBlockIndex = _seqNumToBlockIdxMap[sequenceNumber];
        }

        ////////////////////////////////////////////////////////////////////////
        // FanucGCodeParserBaseListener implementation

        private uint _tempBlockNum = 0; // used for constructing the sequence number jump table
        public override void ExitBlock([NotNull] FanucGCodeParser.BlockContext context)
        {
            if (context.sequenceNumber() != null)
                _seqNumToBlockIdxMap[uint.Parse(context.sequenceNumber().DIGITS().GetText())] = _tempBlockNum;

            _tempBlockNum++;
            base.ExitBlock(context);
        }

        public override void EnterProgram([NotNull] FanucGCodeParser.ProgramContext context)
        {
            _name = $"O{context.programNumber().DIGITS().GetText()}";
            base.EnterProgram(context);
        }

        ////////////////////////////////////////////////////////////////////////
        // IAntlrErrorListener<IToken> implementation

        private List<GCodeException> _parseErrors;
        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            _parseErrors.Add(new GCodeException(msg, _name, line, charPositionInLine));
        }
    }
}
