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
    using Microsoft.VisualStudio.Language.StandardClassification;
    using GCodeParser;
    using System.Diagnostics;
    using Microsoft.VisualStudio.Shell;

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
                               ITagAggregator<FanucGCodeTokenTag> fanucGCodeTagAggregator, 
                               IClassificationTypeRegistryService typeService)
        {
            // I found an example of how to use builtin classifications here:
            // https://github.com/ponylang/VS-pony/blob/master/PonyLanguage/SyntaxHighlighter.cs

            // standard theme mapping:
            // PredefinedClassificationTypeNames.Comment is green
            // PredefinedClassificationTypeNames.Keyword is blue
            // PredefinedClassificationTypeNames.SymbolDefinition is green-blue (like C++/C# custom type)
            // PredefinedClassificationTypeNames.SymbolReference is bold dark green
            // PredefinedClassificationTypeNames.PreprocessorKeyword is light grey
            // PredefinedClassificationTypeNames.Identifier is uncoloured
            // PredefinedClassificationTypeNames.Operator is uncoloured
            // PredefinedClassificationTypeNames.String is red

            _buffer = buffer;
            _aggregator = fanucGCodeTagAggregator;
            _fanucGCodeTypes = new Dictionary<FanucGCodeTokenTypes, IClassificationType>();
            // note several different token types will map to the same classification type, eg the scanner will
            // pick out components of a comment including brackets, but we want the whole thing to be classified
            // as a comment editor format.
            _fanucGCodeTypes[FanucGCodeTokenTypes.CommentStart] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            _fanucGCodeTypes[FanucGCodeTokenTypes.CommentText] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            _fanucGCodeTypes[FanucGCodeTokenTypes.CommentEnd] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Comment);

            // functions - should possibly have their own format
            _fanucGCodeTypes[FanucGCodeTokenTypes.BuiltinFunction] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
            _fanucGCodeTypes[FanucGCodeTokenTypes.AxNum_Function] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
            _fanucGCodeTypes[FanucGCodeTokenTypes.Ax_Function] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
            _fanucGCodeTypes[FanucGCodeTokenTypes.SetVN_Function] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
            _fanucGCodeTypes[FanucGCodeTokenTypes.BPrnt_Function] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
            _fanucGCodeTypes[FanucGCodeTokenTypes.DPrnt_Function] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
            _fanucGCodeTypes[FanucGCodeTokenTypes.POpen_Function] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
            _fanucGCodeTypes[FanucGCodeTokenTypes.PClos_Function] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);

            // keywords
            _fanucGCodeTypes[FanucGCodeTokenTypes.If] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            _fanucGCodeTypes[FanucGCodeTokenTypes.Then] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            _fanucGCodeTypes[FanucGCodeTokenTypes.Goto] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            _fanucGCodeTypes[FanucGCodeTokenTypes.While] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            _fanucGCodeTypes[FanucGCodeTokenTypes.Do] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            _fanucGCodeTypes[FanucGCodeTokenTypes.End] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);

            // relational and logical operators, and modulus operator which has text rather than symbol
            _fanucGCodeTypes[FanucGCodeTokenTypes.RelationalOperator] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            _fanucGCodeTypes[FanucGCodeTokenTypes.LogicalOperator] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            _fanucGCodeTypes[FanucGCodeTokenTypes.Modulus] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);

            // system vars, constants and common vars should have their text coloured like C# string
            _fanucGCodeTypes[FanucGCodeTokenTypes.NamedVariable] = typeService.GetClassificationType(PredefinedClassificationTypeNames.String);

            // GCode prefixes need to stand out in the code
            _fanucGCodeTypes[FanucGCodeTokenTypes.ProgramNumberPrefix] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
            _fanucGCodeTypes[FanucGCodeTokenTypes.LabelPrefix] = typeService.GetClassificationType(PredefinedClassificationTypeNames.String);
            _fanucGCodeTypes[FanucGCodeTokenTypes.GCodePrefix] = typeService.GetClassificationType(PredefinedClassificationTypeNames.SymbolDefinition);
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
