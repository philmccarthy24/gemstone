using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace FanucGCodeLanguage
{
    // I suspect this might need to have all the token types? Not sure how the parsing will work yet, for when we need to recognise errors.
    // there will probably be a many to one mapping between ClassificationTypeDefinitions and ClassificationFormatDefinition instances??
    internal static class OrdinaryClassificationDefinition
    {
        #region Type definition

        /// <summary>
        /// Defines the "fanucGCodeComment" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("GCodeComment")]
        internal static ClassificationTypeDefinition GCodeComment = null;

        /// <summary>
        /// Defines the "fanucGCodeKeyword" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("GCodeKeyword")]
        internal static ClassificationTypeDefinition GCodeKeyword = null;

        #endregion
    }
}
