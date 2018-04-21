using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace FanucGCodeLanguage
{
    #region Format definition
    /// <summary>
    /// Defines the editor format for the GCodeComment classification type. Text is colored DarkGreen
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "GCodeComment")]
    [Name("GCodeComment")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class GCodeCommentFormat : ClassificationFormatDefinition
    {
        /// <summary>
        /// Defines the visual format for the "exclamation" classification type
        /// </summary>
        public GCodeCommentFormat()
        {
            DisplayName = "Comment"; //human readable version of the name
            ForegroundColor = Colors.Green;
        }
    }

    /// <summary>
    /// Defines the editor format for the GCodeKeyword classification type. Text is colored DarkBlue
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "GCodeKeyword")]
    [Name("GCodeKeyword")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class GCodeKeywordFormat : ClassificationFormatDefinition
    {
        /// <summary>
        /// Defines the visual format for the "exclamation" classification type
        /// </summary>
        public GCodeKeywordFormat()
        {
            DisplayName = "GCodeKeyword"; //human readable version of the name
            ForegroundColor = Colors.Blue;
        }
    }
    #endregion //Format definition
}
