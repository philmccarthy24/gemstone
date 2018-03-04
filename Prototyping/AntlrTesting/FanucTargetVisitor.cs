using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;

namespace AntlrTesting
{
    // Each visitor derivation is controller target specific. This example one is for Fanuc.
    //
    // Note it would be a lot better if the visitor methods has responsibility for rendering the
    // corresponding AST node's subtree in the target representation, and returned string output
    // rather than using a class member StringBuilder. Otherwise get repetition of variable check logic etc.
    // This is an artifact of me learning how to use AnTLR...
    class FanucTargetVisitor : GemGrammarBaseVisitor<string>
    {
        public StringBuilder Output { get; set; } = new StringBuilder();
        private int _nextGoto = 100;

        // table of friendly handles to #variables they represent
        private Dictionary<string, string> _varTable = new Dictionary<string, string>();

        private int _dummyNextFreeLocalVar = 1;

        private string NextFreeLocal()
        {
            // dummy implementation - a proper one would have to account for reserved variables, input
            // params used in the G65 call, and what blocks we have free (possibly indicated via config)
            return string.Format("#{0}", _dummyNextFreeLocalVar++);
        }

        // TODO: escaping nested parentheses - does \ work in gcode? I have just removed them for now.
        // this is probably fairly similar to the final impl
        public override string VisitComment(GemGrammarParser.CommentContext context)
        {
            if (context.SingleLineComment() != null)
            {
                var commentLine = context.SingleLineComment().GetText();
                // trim start
                commentLine = commentLine.TrimStart(new char[] { '/' });
                commentLine = commentLine.Trim();
                // remove parentheses
                commentLine = commentLine.Replace("(", "");
                commentLine = commentLine.Replace(")", "");
                Output.AppendFormat("( {0} )", commentLine);
                Output.AppendLine();
            }
            else if (context.MultiLineComment() != null)
            {
                string comment = context.MultiLineComment().GetText().Trim(new char[] { '/', '*' });
                var commentLines = comment.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in commentLines)
                {
                    // remove parentheses
                    var escapedLine = line.Replace("(", "");
                    escapedLine = escapedLine.Replace(")", "");
                    Output.AppendFormat("( {0} )", escapedLine);
                    Output.AppendLine();
                }
            }
            else throw new InvalidOperationException("Assertion failed: Both comments null in comment visitor");
            return "";
        }

        // shows how if/else could work for Fanuc
        public override string VisitIfStatement(GemGrammarParser.IfStatementContext context)
        {
            int ifBranchLabel = _nextGoto+=10;
            int elseBranchLabel = _nextGoto+=10;

            Output.Append("IF [");
            // process the condition subtree
            var conditionContext = context.expressionSequence();
            Visit(conditionContext);
            Output.AppendFormat("] GOTO {0}", ifBranchLabel);
            Output.AppendLine();

            // now we want to process the else block, if it's present
            if (context.Else() != null)
            {
                Visit(context.statement(1));
            }
            // always output goto else label
            Output.AppendFormat("GOTO {0}", elseBranchLabel);
            Output.AppendLine();

            // insert the label the if will jump to
            Output.AppendFormat("N{0}", ifBranchLabel);
            Output.AppendLine();

            // process the if block
            Visit(context.statement(0));

            // insert the else label
            Output.AppendFormat("N{0}", elseBranchLabel);
            Output.AppendLine();

            return "";
        }

        public override string VisitEqualityOperator(GemGrammarParser.EqualityOperatorContext context)
        {
            if (context.NotEquals() != null)
                Output.Append(" NE ");
            else if (context.Equals_() != null)
                Output.Append(" EQ ");
            else throw new InvalidOperationException("unexpected");
            return "";
        }

        public override string VisitAssignmentExpression([NotNull] GemGrammarParser.AssignmentExpressionContext context)
        {
            var varAssignedTo = context.Identifier().GetText();
            if (!_varTable.ContainsKey(varAssignedTo))
                _varTable[varAssignedTo] = NextFreeLocal();

            Output.AppendFormat("{0}=", _varTable[varAssignedTo]);

            // visit the rhs of the assignment AST node
            Visit(context.singleExpression());

            Output.AppendLine();

            return "";
        }

        public override string VisitLiteralExpression([NotNull] GemGrammarParser.LiteralExpressionContext context)
        {
            if (context.literal().NullLiteral() != null)
                Output.Append("#0");
            else if (context.literal().BooleanLiteral() != null)
                Output.Append(context.literal().BooleanLiteral().GetText() == "true" ? "1" : "0");
            else Output.Append(context.literal().GetText());

            return "";
        }

        public override string VisitIdentifierExpression([NotNull] GemGrammarParser.IdentifierExpressionContext context)
        {
            // verify that we've seen the identifier before
            if (!_varTable.ContainsKey(context.GetText()))
                throw new InvalidOperationException(string.Format("Uninitialised variable '{0}': Line {1}, column {2}", context.GetText(), context.Start.Line, context.Start.Column));

            Output.Append(_varTable[context.GetText()]);
            
            return "";
        }

        // if this was better designed, we would use the identifier visitor to return the symbol directly, and also do the var-is-initialised checking.
        // ie re-use parts of what have already been written. for a POC though, this is ok
        public override string VisitPostIncrementExpression([NotNull] GemGrammarParser.PostIncrementExpressionContext context)
        {
            var identifier = context.Identifier().GetText();
            // verify that we've seen the identifier before
            if (!_varTable.ContainsKey(identifier))
                throw new InvalidOperationException(string.Format("Uninitialised variable '{0}': Line {1}, column {2}", identifier, context.Start.Line, context.Start.Column));

            var actualVar = _varTable[identifier];
            Output.AppendFormat("{0}={0}+1", actualVar);
            Output.AppendLine();

            return "";
        }

        public override string VisitPostDecreaseExpression([NotNull] GemGrammarParser.PostDecreaseExpressionContext context)
        {
            var identifier = context.Identifier().GetText();
            // verify that we've seen the identifier before
            if (!_varTable.ContainsKey(identifier))
                throw new InvalidOperationException(string.Format("Uninitialised variable '{0}': Line {1}, column {2}", identifier, context.Start.Line, context.Start.Column));

            var actualVar = _varTable[identifier];
            Output.AppendFormat("{0}={0}-1", actualVar);
            Output.AppendLine();

            return "";
        }

        public override string VisitNotExpression([NotNull] GemGrammarParser.NotExpressionContext context)
        {
            Visit(context.singleExpression());
            Output.Append(" NE 0");

            return "";
        }

        public override string VisitRelationalExpression([NotNull] GemGrammarParser.RelationalExpressionContext context)
        {
            var gt = context.relationalOperator().MoreThan();
            var lt = context.relationalOperator().LessThan();
            var gte = context.relationalOperator().GreaterThanEquals();
            var lte = context.relationalOperator().LessThanEquals();
            var op = (gt != null ? " GT " : (lt != null ? " LT " : (gte != null ? " GE " : (lte != null ? " LE " : null))));
            if (op == null)
                throw new InvalidOperationException(string.Format("bad thing happened"));

            Visit(context.singleExpression(0));
            Output.Append(op);
            Visit(context.singleExpression(1));
            return "";
        }

        public override string VisitGcode([NotNull] GemGrammarParser.GcodeContext context)
        {
            var gcodeId = context.GCodeId().GetText();
            Output.Append(gcodeId);
            foreach (var gcodeParam in context.gcodeParamExpr())
            {
                if (gcodeParam.Identifier() == null)
                {
                    // param contains a literal
                    Output.Append(gcodeParam.GetText());
                }
                else
                {
                    // get the #var for the identifier passed in
                    Output.Append(gcodeParam.GCodeParam().GetText());
                    var identifier = gcodeParam.Identifier().GetText();
                    if (!_varTable.ContainsKey(identifier))
                        throw new InvalidOperationException(string.Format("Uninitialised variable '{0}': Line {1}, column {2}", identifier, context.Start.Line, context.Start.Column));
                    Output.Append(_varTable[identifier]);
                }
            }
            Output.AppendLine();
            return "";
        }
    }
}
