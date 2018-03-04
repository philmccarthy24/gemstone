using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

/*
GCode
Exporting
Meta
Script
TOol for
NumEric Controllers

GEMSTONE

NOTE
Identifier is still being mis-used by the grammar as a variable token - actually it would also be used for things
such as struct type names, and function names.
Thus we still have an issue with token ambiguity for gcode statements versus identifiers.
Might be able to write some semantic predecates for functions and types? then use a variable token which is '$' Identifier?
Don't know - needs further thought. Disambiguation seems to be a tradeoff versus clarity of source input.
Think special chars to denote a gcode line should be avoided!
*/

namespace AntlrTesting
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "AntlrTesting.testscript.gem";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    AntlrInputStream inputStream = new AntlrInputStream(stream);
                    GemGrammarLexer gemLexer = new GemGrammarLexer(inputStream);
                    CommonTokenStream commonTokenStream = new CommonTokenStream(gemLexer);

                    /* print out all the tokens, for debugging lexer grammar.
                    commonTokenStream.Fill();
                    var tokens = commonTokenStream.GetTokens();
                    foreach (var token in tokens)
                    {
                        Console.WriteLine(string.Format("{0}: {1}\n", gemLexer.Vocabulary.GetSymbolicName(token.Type), token.Text));
                    }
                    */

                    GemGrammarParser gemParser = new GemGrammarParser(commonTokenStream);

                    GemGrammarParser.ProgramContext progContext = gemParser.program();
                    FanucTargetVisitor visitor = new FanucTargetVisitor();
                    visitor.Visit(progContext);

                    Console.WriteLine(visitor.Output.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex);
            }
        }
    }
}
