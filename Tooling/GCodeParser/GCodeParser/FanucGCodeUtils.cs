﻿using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GCodeParser
{

    // TODO: Ruminations 15/06/18
    // I think the intent of this is different to the core parser. I think for language syntax highlighting for VS plugin etc,
    // this should be a very cut down list of general classifications, eg Keyword, BuiltinFunction, ArithmeticOperator, LogicalOperator etc.
    // There still needs to be a syntax checker that returns tagged input text in error.
    // The GCode.LanguageTools.Fanuc namespace should possibly be used to house these classes, keeping the generated lexer/parser in GCode.Parsing.Fanuc?
    // and the interpreter in GCode.Interpreter.Fanuc.
    // This seperates out the functionality and intent.

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
        Integer = FanucGCodeLexer.DIGITS,
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
        EndOfBlock = FanucGCodeLexer.NEWLINE,
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

    public class FanucGCodeParseError
    {
        public FanucGCodeTextSpan OffendingSymbol { get; set; }
        public int Line { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return $"Error: Line {Line}, Col {OffendingSymbol.StartPos}. {Message}";
        }
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

    public class FanucGCodeSyntaxChecker : IAntlrErrorListener<IToken>
    {
        private List<FanucGCodeParseError> _parseErrors;

        public List<FanucGCodeParseError> CheckSyntax(string gcodeContent, bool programContext = false)
        { 
            AntlrInputStream inputStream = new AntlrInputStream(gcodeContent);
            FanucGCodeLexer fanucLexer = new FanucGCodeLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(fanucLexer);

            FanucGCodeParser fanucParser = new FanucGCodeParser(commonTokenStream);

            _parseErrors = new List<FanucGCodeParseError>();

            fanucParser.RemoveErrorListeners();
            fanucParser.AddErrorListener(this);

            if (programContext)
            {
                // this line triggers the actual parse.
                var progContext = fanucParser.program();
            }
            else
            {
                var blocksContext = fanucParser.programContent();

                Debug.WriteLine($"Checking syntax of '{gcodeContent.Trim()}'");
                Debug.WriteLine($"Which has parse tree:\n{blocksContext.ToStringTree(fanucParser)}");
            }

            /*FanucTargetVisitor visitor = new FanucTargetVisitor();
            visitor.Visit(progContext);

            Console.WriteLine(visitor.Output.ToString());
            */

            foreach (var error in _parseErrors)
            {
                Debug.WriteLine(error.ToString());
            }

            return _parseErrors;
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            _parseErrors.Add(new FanucGCodeParseError
            {
                OffendingSymbol = new FanucGCodeTextSpan
                {
                    StartPos = offendingSymbol.StartIndex,
                    Length = (offendingSymbol.StopIndex - offendingSymbol.StartIndex) + 1,
                    TokenType = (FanucGCodeTokenTypes)offendingSymbol.Type
                },
                Line = line,
                Message = msg
            });
        }
    }
}
