using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace VSGCodeLangSupport.Validation
{

    // I got the framework for the code below from
    // https://github.com/madskristensen/ExtensibilityTools/blob/master/src/Pkgdef/Validation/PkgdefErrorTagger.cs
    // This seems to work nicely and doesn't need additional assemblies added, though margin adornments on right hand side
    // still only show one intellisense error (instead of all of them), and it takes quite a while for all error tasks to be added to
    // list: this isn't parsing all in one go (we can get away with line-by-line for GCode, but Gem is another matter).
    // The Dafny extension might shine some further light on things, for how to take this forward.


    // some general notes and links I took when researching how to do intellisense parse errors follows:
    //=====================================================================================================

    // Take a look at https://github.com/Microsoft/dafny/blob/master/Source/DafnyExtension/ErrorTagger.cs
    // for possibly how to do error tagging properly. The comment in the source also says that it uses
    // the Error List - don't know if that means intellisense error? Anyway might be worth taking a look
    // at what this code is doing.


    // So this definitely works (with a com error that breaks VS on exit?). It seems the GetTags function gets called as the text view is being scrolled, with snapshots
    // corresponding to the text currently shown on screen.
    // We need a way to 1) trigger ~globally a parse of the entire doc, and add errors to the error list as per below
    // 2) be notified of changes to the ITextBuffer, and reparse/remove+re-add errors
    // 3) return error tags as requested from this tagger

    // see http://blog.diniscruz.com/2012/07/adding-item-to-visualstudios-error-list.html for more info


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

    // the positional adornment also seems to only be displayed for the first error, and isn't displayed for all the others (however red squiggles are shown for all ']' chars).
*/
// end
// =================

    class FanucGCodeErrorTagger : ITagger<IErrorTag>, IDisposable
    {
        private IClassifier _classifier;
        private bool _disposed = false;
        private ErrorListProvider _errorlist;
        private ITextDocument _document;
        private IWpfTextView _view;

        public FanucGCodeErrorTagger(IWpfTextView view, IClassifierAggregatorService classifier, ErrorListProvider errorlist, ITextDocument document)
        {
            _view = view;
            _classifier = classifier.GetClassifier(view.TextBuffer);
            _errorlist = errorlist;
            _document = document;
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (SnapshotSpan currSpan in spans)
            {
                // mark all occurences of ']]' as an intellisense error: here's where we would insert GCode parse code
                int loc = currSpan.GetText().ToLower().IndexOf("]]");

                if (loc > -1)
                {
                    SnapshotSpan CheckTextSpan = new SnapshotSpan(currSpan.Snapshot, new Span(currSpan.Start + loc, 2));

                    var line = CheckTextSpan.Start.GetContainingLine();

                    ClearError(line);

                    yield return CreateError(line, CheckTextSpan, "The reactor containment system has failed.");
                }
            }
        }

        /*
                    var span = spans[0];
            var line = span.Start.GetContainingLine();
            var classificationSpans = _classifier.GetClassificationSpans(line.Extent);

            ClearError(line);

            foreach (var cspan in classificationSpans)
            {
                if (cspan.ClassificationType.IsOfType(PredefinedClassificationTypeNames.SymbolDefinition))
                {
                    string text = cspan.Span.GetText();

                    if (text.StartsWith("$", StringComparison.Ordinal))
                    {
                        string word = text.Trim('$');

                        if (!CompletionItem.Items.Any(i => i.Name.Equals(word, StringComparison.OrdinalIgnoreCase)))
                            yield return CreateError(line, cspan.Span, "The keyword '$" + word + "$' doesn't exist");
                    }
                }

                if (cspan.ClassificationType.IsOfType(PkgdefClassificationTypes.Guid))
                {
                    string text = cspan.Span.GetText();
                    Guid guid;

                    if (!Guid.TryParse(text, out guid))
                    {
                        yield return CreateError(line, cspan.Span, "\"" + text + "\" is not a valid GUID.");
                    }
                }

                else if (cspan.ClassificationType.IsOfType(PkgdefClassificationTypes.RegistryPath))
                {
                    string lineText = line.GetText();

                    var match = Variables.RegistryPath.Match(lineText);
                    if (!match.Success)
                        break;

                    var group = match.Groups["path"];
                    string path = group.Value;

                    if (span.Snapshot.Length <= span.Start.Position + group.Index + group.Length)
                        break;

                    var hit = new SnapshotSpan(span.Snapshot, span.Start + group.Index, group.Length);

                    if (path.Trim().Length < path.Length)
                        yield return CreateError(line, hit, "Remove whitespace around the registry key path");

                    else if (string.IsNullOrWhiteSpace(path))
                        yield return CreateError(line, cspan.Span, "You must specify a registry key path");

                    else if (!match.Value.EndsWith("]"))
                        yield return CreateError(line, hit, "Unclosed registry key entry. Add the missing ] character");

                    else if (cspan.Span.GetText().Contains("/") && !cspan.Span.GetText().Contains("\\/"))
                        yield return CreateError(line, cspan.Span, "Use the backslash character as delimiter instead of forward slash.");
                }

                else if (cspan.ClassificationType.IsOfType(PredefinedClassificationTypeNames.String))
                {
                    string lineText = line.GetText();

                    foreach (Match match in Variables.String.Matches(lineText))
                    {
                        string text = match.Value;

                        if (cspan.Span.Snapshot.Length < span.Start.Position + match.Index + match.Length)
                            continue;

                        var hit = new SnapshotSpan(cspan.Span.Snapshot, span.Start.Position + match.Index, match.Length);

                        if (text.Length <= 1 || text[text.Length - 1] != '"')
                        {
                            yield return CreateError(line, hit, "Unclosed string. Add the missing \" character.");
                        }
                    }
                }
            }
            
        }*/

        private TagSpan<ErrorTag> CreateError(ITextSnapshotLine line, SnapshotSpan span, string message)
        {
            foreach (ErrorTask existing in _errorlist.Tasks)
            {
                if (existing.Line == line.LineNumber && existing.Text.EndsWith(message))
                    return null;
            }

            ErrorTask task = CreateErrorTask(line, span, "GCode Language Tools: " + message);
            _errorlist.Tasks.Add(task);

            return new TagSpan<ErrorTag>(span, new ErrorTag("Syntax error", message));
        }

        private ErrorTask CreateErrorTask(ITextSnapshotLine line, SnapshotSpan span, string text)
        {
            ErrorTask task = new ErrorTask
            {
                Text = text,
                Line = line.LineNumber,
                Column = span.Start.Position - line.Start.Position,
                Category = TaskCategory.CodeSense,
                ErrorCategory = TaskErrorCategory.Warning,
                Priority = TaskPriority.Normal,
                Document = _document.FilePath
            };

            task.Navigate += task_Navigate;

            return task;
        }

        private void task_Navigate(object sender, EventArgs e)
        {
            ErrorTask task = (ErrorTask)sender;
            _errorlist.Navigate(task, new Guid("{00000000-0000-0000-0000-000000000000}"));

            var line = _view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(task.Line);
            var point = new SnapshotPoint(line.Snapshot, line.Start.Position + task.Column);
            _view.Caret.MoveTo(point);
        }

        private void ClearError(ITextSnapshotLine line)
        {
            foreach (ErrorTask existing in _errorlist.Tasks)
            {
                if (existing.Line == line.LineNumber)
                {
                    _errorlist.Tasks.Remove(existing);
                    break;
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                (_classifier as IDisposable)?.Dispose();
            }
        }
    }
}
