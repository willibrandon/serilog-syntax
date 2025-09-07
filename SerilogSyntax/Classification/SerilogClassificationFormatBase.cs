using Microsoft.VisualStudio.Text.Classification;

namespace SerilogSyntax.Classification;

/// <summary>
/// Base class for theme-aware Serilog classification format definitions.
/// Automatically updates colors when Visual Studio theme changes.
/// </summary>
public abstract class SerilogClassificationFormatBase : ClassificationFormatDefinition, ISerilogClassificationDefinition
{
    private readonly SerilogThemeColors _themeColors;
    private readonly string _classificationTypeName;

    /// <summary>
    /// Initializes a new instance of the theme-aware classification format.
    /// </summary>
    /// <param name="themeColors">The theme colors service.</param>
    /// <param name="classificationTypeName">The classification type name.</param>
    /// <param name="displayName">The display name for the format.</param>
    protected SerilogClassificationFormatBase(
        SerilogThemeColors themeColors, 
        string classificationTypeName, 
        string displayName)
    {
        _themeColors = themeColors;
        _classificationTypeName = classificationTypeName;
        DisplayName = displayName;
        
        _themeColors.RegisterClassificationDefinition(this);
        Reinitialize();
    }

    /// <summary>
    /// Reinitializes the classification format colors based on the current Visual Studio theme.
    /// Called automatically when the VS theme changes.
    /// </summary>
    public virtual void Reinitialize()
    {
        var colors = _themeColors.GetColorsForCurrentTheme();
        if (colors.TryGetValue(_classificationTypeName, out var textProperties))
        {
            ForegroundColor = textProperties.Foreground;
            BackgroundColor = textProperties.Background;
            IsBold = textProperties.IsBold;
            IsItalic = textProperties.IsItalic;
        }
    }
}