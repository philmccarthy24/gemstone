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
    [TagType(typeof(ClassificationTag))]
    internal sealed class FanucGCodeClassifierProvider : ITaggerProvider
    {

        [Export]
        [Name("FanucGCode")]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition FanucGCodeContentType = null;

        [Export]
        [FileExtension(".prg")]
        [ContentType("FanucGCode")]
        internal static FileExtensionToContentTypeDefinition FanucGCodeFileType = null;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry = null;

        [Import]
        internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {

            ITagAggregator<FanucGCodeTokenTag> fanucGCodeTagAggregator = 
                                            aggregatorFactory.CreateTagAggregator<FanucGCodeTokenTag>(buffer);

            return new FanucGCodeClassifier(buffer, fanucGCodeTagAggregator, ClassificationTypeRegistry) as ITagger<T>;
        }
    }

    internal sealed class FanucGCodeClassifier : ITagger<ClassificationTag>
    {
        ITextBuffer _buffer;
        ITagAggregator<FanucGCodeTokenTag> _aggregator;
        IDictionary<FanucGCodeTokenTypes, IClassificationType> _fanucGCodeTypes;

        /// <summary>
        /// Construct the classifier and define search tokens
        /// </summary>
        internal FanucGCodeClassifier(ITextBuffer buffer, 
                               ITagAggregator<FanucGCodeTokenTag> ookTagAggregator, 
                               IClassificationTypeRegistryService typeService)
        {
            _buffer = buffer;
            _aggregator = ookTagAggregator;
            _fanucGCodeTypes = new Dictionary<FanucGCodeTokenTypes, IClassificationType>();
            // note several different token types will map to the same classification type, eg the scanner will
            // pick out components of a comment including brackets, but we want the whole thing to be classified
            // as a comment editor format.
            _fanucGCodeTypes[FanucGCodeTokenTypes.CommentStart] = typeService.GetClassificationType("GCodeComment");
            _fanucGCodeTypes[FanucGCodeTokenTypes.CommentText] = typeService.GetClassificationType("GCodeComment");
            _fanucGCodeTypes[FanucGCodeTokenTypes.CommentEnd] = typeService.GetClassificationType("GCodeComment");

            // functions - should possibly have their own format
            _fanucGCodeTypes[FanucGCodeTokenTypes.BuiltinFunction] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.AxNum_Function] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.Ax_Function] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.SetVN_Function] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.BPrnt_Function] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.DPrnt_Function] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.POpen_Function] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.PClos_Function] = typeService.GetClassificationType("GCodeKeyword");

            // keywords
            _fanucGCodeTypes[FanucGCodeTokenTypes.If] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.Then] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.Goto] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.While] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.Do] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.End] = typeService.GetClassificationType("GCodeKeyword");

            // relational and logical operators, and modulus operator which has text rather than symbol
            _fanucGCodeTypes[FanucGCodeTokenTypes.RelationalOperator] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.LogicalOperator] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.Modulus] = typeService.GetClassificationType("GCodeKeyword");

            // system vars, constants and common vars should have their text coloured like C# string
            _fanucGCodeTypes[FanucGCodeTokenTypes.NamedVariable] = typeService.GetClassificationType("GCodeKeyword");

            // GCode prefixes need to stand out in the code
            _fanucGCodeTypes[FanucGCodeTokenTypes.ProgramNumberPrefix] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.LabelPrefix] = typeService.GetClassificationType("GCodeKeyword");
            _fanucGCodeTypes[FanucGCodeTokenTypes.GCodePrefix] = typeService.GetClassificationType("GCodeKeyword");
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        /// <summary>
        /// Search the given span for any instances of classified tags
        /// </summary>
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (var tagSpan in _aggregator.GetTags(spans))
            {
                var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                yield return 
                    new TagSpan<ClassificationTag>(tagSpans[0], 
                                                   new ClassificationTag(_fanucGCodeTypes[tagSpan.Tag.type]));
            }
        }
    }
}
