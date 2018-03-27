using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amethyst
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var fileStream = new FileStream(args[0], FileMode.Open))
            {
                AntlrInputStream inputStream = new AntlrInputStream(fileStream);
                Python3Lexer pyLexer = new Python3Lexer(inputStream);
                CommonTokenStream commonTokenStream = new CommonTokenStream(pyLexer);

                // print out all the tokens, for debugging lexer grammar.
                commonTokenStream.Fill();
                var tokens = commonTokenStream.GetTokens();
                foreach (var token in tokens)
                {
                    Console.WriteLine(string.Format("{0}: {1}\n", pyLexer.Vocabulary.GetSymbolicName(token.Type), token.Text));
                }

                /*
                Python3Parser pyParser = new Python3Parser(commonTokenStream);

                Python3Parser.File_inputContext progContext = pyParser.file_input();
                
                Console.WriteLine(progContext.ToStringTree());
                */
            }
        }
    }
}
