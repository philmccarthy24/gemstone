using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GCodeParser
{
    // map from antlr token type constants here, so we have a public defined interface and are free to change the internals
    public enum FanucGCodeTokenTypes
    {
        Hash = FanucGCodeLexer.HASH,
        Plus = FanucGCodeLexer.PLUS,
        Minus = FanucGCodeLexer.MINUS,
        Multiply = FanucGCodeLexer.MULTIPLY,
        Divide = FanucGCodeLexer.DIVIDE,
        Modulus = FanucGCodeLexer.MOD,
        Assign = FanucGCodeLexer.EQUALS,
        Integer = FanucGCodeLexer.INTEGER,
        Decimal = FanucGCodeLexer.DECIMAL,
        NamedVariable = FanucGCodeLexer.SYSTEMVAR_CONST_OR_COMMONVAR_IDENTIFIER,
        Comma = FanucGCodeLexer.COMMA,
        OpenBracket = FanucGCodeLexer.OPEN_BRACKET,
        CloseBracket = FanucGCodeLexer.CLOSE_BRACKET,
        StartEndProgram = FanucGCodeLexer.START_END_PROGRAM,
        RelationalOperator = FanucGCodeLexer.RELATIONAL_OP,
        LogicalOperator = FanucGCodeLexer.LOGICAL_OP,
        If = FanucGCodeLexer.IF,
        Then = FanucGCodeLexer.THEN,
        Goto = FanucGCodeLexer.GOTO,
        While = FanucGCodeLexer.WHILE,
        Do = FanucGCodeLexer.DO,
        End = FanucGCodeLexer.END,
        BuiltinFunction = FanucGCodeLexer.BUILTIN_FUNCTION,
        Ax_Function = FanucGCodeLexer.AX,
        AxNum_Function = FanucGCodeLexer.AXNUM,
        SetVN_Function = FanucGCodeLexer.SETVN,
        BPrnt_Function = FanucGCodeLexer.BPRNT,
        DPrnt_Function = FanucGCodeLexer.DPRNT,
        POpen_Function = FanucGCodeLexer.POPEN,
        PClos_Function = FanucGCodeLexer.PCLOS,
        ProgramNumberPrefix = FanucGCodeLexer.PROGRAM_NUMBER_PREFIX,
        LabelPrefix = FanucGCodeLexer.SEQUENCE_NUMBER_PREFIX,
        GCodePrefix = FanucGCodeLexer.GCODE_PREFIX,
        EndOfBlock = FanucGCodeLexer.EOB,
        CommentStart = FanucGCodeLexer.CTRL_OUT,
        CommentText = FanucGCodeLexer.CTRL_OUT_TEXT,
        CommentEnd = FanucGCodeLexer.CTRL_IN,
        Unknown = FanucGCodeLexer.UNRECOGNISED_TEXT
    }

    public class FanucGCodeTextSpan
    {
        public int StartPos { get; set; }
        public int Length { get; set; }
        public FanucGCodeTokenTypes TokenType { get; set; }
    }

    // scanner for input sequences. uses the fanuc lexer
    public class FanucGCodeScanner
    {
        public static IEnumerable<FanucGCodeTextSpan> Tokenise(string testInput)
        {
            AntlrInputStream inputStream = new AntlrInputStream(testInput);
            FanucGCodeLexer fanucLexer = new FanucGCodeLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(fanucLexer);

            commonTokenStream.Fill();
            var tokens = commonTokenStream.GetTokens();

            return tokens.Where(t => t.Type != FanucGCodeLexer.Eof).Select(t => new FanucGCodeTextSpan
            {
                StartPos = t.StartIndex,
                Length = (t.StopIndex - t.StartIndex) + 1,
                TokenType = (FanucGCodeTokenTypes)t.Type
            });
        }
    }

    partial class FanucGCodeLexer
    {
        
    }
}
