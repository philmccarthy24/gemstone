namespace FanucGCodeLanguage
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;
    using GCodeParser;

    [Export(typeof(ITaggerProvider))]
    [ContentType("FanucGCode")]
    [TagType(typeof(FanucGCodeTokenTag))]
    internal sealed class FanucGCodeTokenTagProvider : ITaggerProvider
    {

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new FanucGCodeTokenTagger(buffer) as ITagger<T>;
        }
    }

    public class FanucGCodeTokenTag : ITag 
    {
        public FanucGCodeTokenTypes type { get; private set; }

        public FanucGCodeTokenTag(FanucGCodeTokenTypes type)
        {
            this.type = type;
        }
    }

    internal sealed class FanucGCodeTokenTagger : ITagger<FanucGCodeTokenTag>
    {
        ITextBuffer _buffer;
        // when we support tagging all the token types, this will no longer be necessary - we can just check for !Unknown
        ISet<FanucGCodeTokenTypes> _supportedTypes = new HashSet<FanucGCodeTokenTypes>()
        {
            FanucGCodeTokenTypes.CommentStart,
            FanucGCodeTokenTypes.CommentText,
            FanucGCodeTokenTypes.CommentEnd,
            FanucGCodeTokenTypes.BuiltinFunction,
            FanucGCodeTokenTypes.AxNum_Function,
            FanucGCodeTokenTypes.Ax_Function,
            FanucGCodeTokenTypes.SetVN_Function,
            FanucGCodeTokenTypes.BPrnt_Function,
            FanucGCodeTokenTypes.DPrnt_Function,
            FanucGCodeTokenTypes.POpen_Function,
            FanucGCodeTokenTypes.PClos_Function,
            FanucGCodeTokenTypes.If,
            FanucGCodeTokenTypes.Then,
            FanucGCodeTokenTypes.Goto,
            FanucGCodeTokenTypes.While,
            FanucGCodeTokenTypes.Do,
            FanucGCodeTokenTypes.End,
            FanucGCodeTokenTypes.RelationalOperator,
            FanucGCodeTokenTypes.LogicalOperator,
            FanucGCodeTokenTypes.Modulus,
            FanucGCodeTokenTypes.NamedVariable,
            FanucGCodeTokenTypes.ProgramNumberPrefix,
            FanucGCodeTokenTypes.LabelPrefix,
            FanucGCodeTokenTypes.GCodePrefix
        };

        internal FanucGCodeTokenTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<FanucGCodeTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {

            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();
                int curLoc = containingLine.Start.Position;

                var tokens = FanucGCodeScanner.Tokenise(containingLine.GetText());

                foreach (var fanucGCodeToken in tokens)
                {
                    // tag known tokens only. when all valid tokens are supported for tagging, can just have:
                    //if (fanucGCodeToken.TokenType != FanucGCodeTokenTypes.Unknown) here.
                    if (_supportedTypes.Contains(fanucGCodeToken.TokenType))
                    {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc + fanucGCodeToken.StartPos, fanucGCodeToken.Length));
                        if( tokenSpan.IntersectsWith(curSpan) ) 
                            yield return new TagSpan<FanucGCodeTokenTag>(tokenSpan, 
                                                                  new FanucGCodeTokenTag(fanucGCodeToken.TokenType));
                    }
                }
            }
            
        }
    }
}
