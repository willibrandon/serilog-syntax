using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using SerilogSyntax.Diagnostics;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Tagging;

/// <summary>
/// Provides property-argument highlighter taggers for C# text views.
/// </summary>
[Export(typeof(IViewTaggerProvider))]
[ContentType("CSharp")]
[TagType(typeof(TextMarkerTag))]
internal sealed class PropertyArgumentHighlighterProvider : IViewTaggerProvider
{
    /// <summary>
    /// Creates a tagger for property-argument highlighting in the specified text view and buffer.
    /// </summary>
    /// <typeparam name="T">The type of tag.</typeparam>
    /// <param name="textView">The text view.</param>
    /// <param name="buffer">The text buffer.</param>
    /// <returns>A property-argument highlighter tagger, or null if the tag type is not supported.</returns>
    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        DiagnosticLogger.Log($"PropertyArgumentHighlighterProvider.CreateTagger: Called for buffer type {buffer?.ContentType?.TypeName ?? "null"}");

        if (buffer == null || textView == null)
        {
            DiagnosticLogger.Log("PropertyArgumentHighlighterProvider.CreateTagger: Buffer or view is null, returning null");
            return null;
        }

        // Only provide highlighting for the top-level buffer
        if (textView.TextBuffer != buffer)
        {
            DiagnosticLogger.Log("PropertyArgumentHighlighterProvider.CreateTagger: Not top-level buffer, returning null");
            return null;
        }

        // Create or get the highlight state for this view
        var highlightState = textView.Properties.GetOrCreateSingletonProperty(
            typeof(PropertyArgumentHighlightState),
            () => new PropertyArgumentHighlightState(textView));

        DiagnosticLogger.Log("PropertyArgumentHighlighterProvider.CreateTagger: Creating PropertyArgumentHighlighter");
        return new PropertyArgumentHighlighter(textView, buffer, highlightState) as ITagger<T>;
    }
}