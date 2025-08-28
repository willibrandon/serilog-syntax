using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Classification
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("CSharp")]
    [Name("Serilog Classifier")]
    internal class SerilogClassifierProvider : IClassifierProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry { get; set; }

        private static SerilogClassifier _classifier;

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            if (_classifier == null)
            {
                _classifier = new SerilogClassifier(buffer, ClassificationRegistry);
            }
            return _classifier;
        }
    }
}