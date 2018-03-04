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
