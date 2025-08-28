namespace SerilogSyntax.Parsing
{
    internal class TemplateProperty
    {
        public string Name { get; set; }
        public int StartIndex { get; set; }
        public int Length { get; set; }
        public PropertyType Type { get; set; }
        public string FormatSpecifier { get; set; }
        public int FormatStartIndex { get; set; }
        public int BraceStartIndex { get; set; }
        public int BraceEndIndex { get; set; }
        public int OperatorIndex { get; set; }
        public string Alignment { get; set; }
        public int AlignmentStartIndex { get; set; }
    }

    internal enum PropertyType
    {
        Standard,        // {Property}
        Destructured,    // {@Property}
        Stringified,     // {$Property}
        Positional       // {0}, {1}, etc.
    }
}