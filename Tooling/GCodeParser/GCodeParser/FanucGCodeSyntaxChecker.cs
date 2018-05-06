using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GCodeParser
{

    public class FanucGCodeSyntaxChecker : IAntlrErrorListener<IToken>
    {

        public void CheckSyntax(string gcodeContent)
        { 
            AntlrInputStream inputStream = new AntlrInputStream(gcodeContent);
            FanucGCodeLexer fanucLexer = new FanucGCodeLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(fanucLexer);

            /* print out all the tokens, for debugging lexer grammar.
            commonTokenStream.Fill();
            var tokens = commonTokenStream.GetTokens();
            foreach (var token in tokens)
            {
            Console.WriteLine(string.Format("{0}: {1}\n", gemLexer.Vocabulary.GetSymbolicName(token.Type), token.Text));
            }
            */

            FanucGCodeParser fanucParser = new FanucGCodeParser(commonTokenStream);

            fanucParser.RemoveErrorListeners();
            fanucParser.AddErrorListener(this);


            // this line seems to trigger the actual parse.
            FanucGCodeParser.ProgramContext progContext = fanucParser.program();

            /*FanucTargetVisitor visitor = new FanucTargetVisitor();
            visitor.Visit(progContext);

            Console.WriteLine(visitor.Output.ToString());
            */
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Debug.WriteLine($"Error in parser at line {line}, position {charPositionInLine}: {msg}");
        }
    }
}
