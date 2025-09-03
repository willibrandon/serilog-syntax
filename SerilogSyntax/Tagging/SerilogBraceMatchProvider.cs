using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Tagging;

/// <summary>
/// Provides brace matching taggers for Serilog template properties.
/// </summary>
[Export(typeof(IViewTaggerProvider))]
[ContentType("CSharp")]
[TagType(typeof(TextMarkerTag))]
internal sealed class SerilogBraceMatcherProvider : IViewTaggerProvider
{
    /// <summary>
    /// Creates a tagger for brace matching in Serilog templates.
    /// </summary>
    /// <typeparam name="T">The type of tag.</typeparam>
    /// <param name="textView">The text view.</param>
    /// <param name="buffer">The text buffer.</param>
    /// <returns>A brace matching tagger, or null if the parameters are invalid.</returns>
    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        if (textView == null || buffer == null || buffer != textView.TextBuffer)
            return null;

        return new SerilogBraceMatcher(textView, buffer) as ITagger<T>;
    }
}
