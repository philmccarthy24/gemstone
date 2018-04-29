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
    using EnvDTE;

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

    // Take a look at https://github.com/Microsoft/dafny/blob/master/Source/DafnyExtension/ErrorTagger.cs
    // for possibly how to do error tagging properly. The comment in the source also says that it uses
    // the Error List - don't know if that means intellisense error? Anyway might be worth taking a look
    // at what this code is doing.

    // Error provider for putting red squiggles under tags that are incorrect
    [Export(typeof(ITaggerProvider))]
    [ContentType("FanucGCode")]
    [TagType(typeof(ErrorTag))]
    class FanucGCodeErrorProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
            {
                throw new ArgumentException("Buffer is null");
            }
            else
            {
                return new FanucGCodeErrorTagger() as ITagger<T>;
            }
        }
    }


    class FanucGCodeErrorTagger : ITagger<IErrorTag>
    {
        private ErrorListProvider _errorProvider = GetErrorListProvider();

        // So this definitely works (with a com error that breaks VS on exit?). It seems the GetTags function gets called as the text view is being scrolled, with snapshots
        // corresponding to the text currently shown on screen.
        // We need a way to 1) trigger ~globally a parse of the entire doc, and add errors to the error list as per below
        // 2) be notified of changes to the ITextBuffer, and reparse/remove+re-add errors
        // 3) return error tags as requested from this tagger

        // see http://blog.diniscruz.com/2012/07/adding-item-to-visualstudios-error-list.html for more info
        public static ErrorListProvider GetErrorListProvider()
        {
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider globalService = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)Package.GetGlobalService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider));
            IServiceProvider serviceProvider = new ServiceProvider(globalService);
            ErrorListProvider errorListProvider = new ErrorListProvider(serviceProvider);
            errorListProvider.ProviderName = "GCode Errors";
            errorListProvider.ProviderGuid = new Guid(Constants.vsViewKindCode);
            return errorListProvider;
        }

        private const string _searchText = "]";

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (SnapshotSpan currSpan in spans)
            {
                int loc = currSpan.GetText().ToLower().IndexOf(_searchText);

                if (loc > -1)
                {
                    // add an error task
                    ErrorTask errorTask = new ErrorTask();
                    errorTask.Category = TaskCategory.CodeSense; // can be CodeSense or BuildCOmpile to show up in the list. Takes a while for errors to show. Also need to re-start VS as get a devenv error: System.Runtime.InteropServices.InvalidComObjectException: COM object that has been separated from its underlying RCW cannot be used.
                    errorTask.Line = 5;//err.LineIndex;
                    errorTask.Column = 2;// err.ColumnIndex;
                    errorTask.Text = "The flib-bob dipped the bloop blab"; //err.Text;
                    errorTask.ErrorCategory = TaskErrorCategory.Error; // has to be "Error" to show up in the list?
                    //errorTask.Document = mFilePath;

                    //errorTask.Navigate += errorTask_Navigate;

                    /* adding the navigate event stopped the errors from appearing
                    errorTask.Navigate += (sender, e) =>
                    {
                        //there are two Bugs in the errorListProvider.Navigate method:
                        //    Line number needs adjusting
                        //    Column is not shown
                        errorTask.Line++;
                        _errorProvider.Navigate(errorTask, new Guid(Constants.vsViewKindCode));
                        errorTask.Line--;
                    };*/

                    _errorProvider.Tasks.Add(errorTask);


                    // READ THIS FOR MORE INFO - NEED TO KNOW WHEN TO PARSE, and how to hold parsed   data


                    SnapshotSpan CheckTextSpan = new SnapshotSpan(currSpan.Snapshot, new Span(currSpan.Start + loc, _searchText.Length));

                // doing research, found this thread:
                //https://social.msdn.microsoft.com/Forums/vstudio/en-US/b1c37c03-f92b-46b1-83f5-7d54a1bbf5e6/how-to-use-error-list-window-in-visual-studio-to-display-error-messages?forum=vsx

                //Sam Harwell recommends this:
                //https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.package.authoringsink?view=visualstudiosdk-2017
                // quote: For that, you should call AddError on the AuthoringSink you create during a ParseSource operation. You should only add the errors when the ParseReason is ParseReason.Check, or you'll find they "randomly" disappear from the Errors window.
                // NOTE THIS WAS WRITTEN IN 2009 SO MAY NOW BE OUT OF DATE (ie might refer to legacy language service)

                // this describes MSBuid error format - might not be useful:
                //https://blogs.msdn.microsoft.com/msbuild/2006/11/02/msbuild-visual-studio-aware-error-messages-and-message-formats/

                    // this is the Get the "Error List Window" code snippet - though requires 'this' to be an IServiceProvider I believe...
                    /*ErrorListProvider errorProvider = new ErrorListProvider(this);
                    Task newError = new Task();
                    newError.Category = TaskCategory.CodeSense;
                    newError.Text = "Some Error Text";
                    errorProvider.Tasks.Add(newError);
                    */

                    // -- this didn't work. how to output an intellisense error??
                    // Console.WriteLine(@"C:\Users\Phil_\Desktop\GemstoneExamplesSource\ExpectedGCode\Fanuc\O9453_verbose.prg(17,20): error PS0168: The variable 'foo' is declared but never used");
                    yield return new TagSpan<ErrorTag>(CheckTextSpan, new ErrorTag("Syntax error", "Bwaaaaah!")); // tried "Compilation error", but lose red error positional adornment on right text window margin
                    // the positional adornment also seems to only be displayed for the first error, and isn't displayed for all the others (however red squiggles are shown for all ']' chars).
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }
    }
}
