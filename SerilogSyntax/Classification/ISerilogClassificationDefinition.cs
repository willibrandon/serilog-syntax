namespace SerilogSyntax.Classification;

/// <summary>
/// Interface for Serilog classification format definitions that support theme-aware color updates.
/// </summary>
public interface ISerilogClassificationDefinition
{
    /// <summary>
    /// Reinitializes the classification format colors based on the current Visual Studio theme.
    /// This method is called automatically when the VS theme changes.
    /// </summary>
    void Reinitialize();
}