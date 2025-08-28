using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

namespace SerilogSyntax.Classification;

/// <summary>
/// Provides instances of <see cref="SerilogClassifier"/> for text buffers.
/// Manages a single classifier instance per buffer using weak references.
/// </summary>
[Export(typeof(IClassifierProvider))]
[ContentType("CSharp")]
[Name("Serilog Classifier")]
internal class SerilogClassifierProvider : IClassifierProvider
{
    /// <summary>
    /// Gets or sets the classification type registry service.
    /// </summary>
    [Import]
    internal IClassificationTypeRegistryService ClassificationRegistry { get; set; }

    /// <summary>
    /// Cache of classifiers per text buffer to ensure one classifier instance per buffer.
    /// Uses weak references to allow garbage collection when buffers are closed.
    /// </summary>
    private readonly ConditionalWeakTable<ITextBuffer, SerilogClassifier> _classifiers = new();

    /// <summary>
    /// Gets a classifier for the given text buffer.
    /// </summary>
    /// <param name="buffer">The text buffer to get a classifier for.</param>
    /// <returns>A <see cref="SerilogClassifier"/> for the given buffer.</returns>
    public IClassifier GetClassifier(ITextBuffer buffer)
    {
        if (!_classifiers.TryGetValue(buffer, out SerilogClassifier classifier))
        {
            classifier = new SerilogClassifier(buffer, ClassificationRegistry);
            _classifiers.Add(buffer, classifier);
        }
        return classifier;
    }
}