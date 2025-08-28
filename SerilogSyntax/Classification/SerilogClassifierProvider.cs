using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

namespace SerilogSyntax.Classification
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("CSharp")]
    [Name("Serilog Classifier")]
    internal class SerilogClassifierProvider : IClassifierProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry { get; set; }

        private readonly ConditionalWeakTable<ITextBuffer, SerilogClassifier> _classifiers = new ConditionalWeakTable<ITextBuffer, SerilogClassifier>();

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
}