using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace VSGCodeLangSupport.Validation
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("FanucGCode")]
    [TagType(typeof(ErrorTag))]
    class FanucGCodeErrorTaggerProvider : ITaggerProvider
    {
        [Import]
        IClassifierAggregatorService _classifierAggregatorService = null;

        [Import]
        public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            ErrorListProvider errorlist;
            if (!buffer.Properties.TryGetProperty(typeof(ErrorListProvider), out errorlist))
                return null;

            IWpfTextView view;
            if (!buffer.Properties.TryGetProperty(typeof(IWpfTextView), out view))
                return null;

            ITextDocument document;
            if (TextDocumentFactoryService.TryGetTextDocument(buffer, out document) && errorlist != null && view != null)
            {
                return new FanucGCodeErrorTagger(view, _classifierAggregatorService, errorlist, document) as ITagger<T>;
            }

            return null;
        }
    }
}
