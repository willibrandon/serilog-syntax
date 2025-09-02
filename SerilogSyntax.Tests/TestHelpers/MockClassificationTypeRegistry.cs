using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;

namespace SerilogSyntax.Tests.TestHelpers;

/// <summary>
/// Mock implementation of IClassificationTypeRegistryService for testing.
/// Provides classification types for Serilog syntax highlighting.
/// </summary>
public class MockClassificationTypeRegistry : IClassificationTypeRegistryService
{
    private readonly Dictionary<string, IClassificationType> _types = [];
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MockClassificationTypeRegistry"/> class.
    /// Registers all Serilog-specific classification types.
    /// </summary>
    private MockClassificationTypeRegistry()
    {
        // Register the Serilog classification types (must match SerilogClassificationTypes constants)
        RegisterType("serilog.property.name");
        RegisterType("serilog.operator.destructure");
        RegisterType("serilog.operator.stringify");
        RegisterType("serilog.format");
        RegisterType("serilog.brace");
        RegisterType("serilog.index");
        RegisterType("serilog.alignment");
    }
    
    /// <summary>
    /// Creates a new instance of the mock classification type registry.
    /// </summary>
    /// <returns>A new IClassificationTypeRegistryService instance for testing.</returns>
    public static IClassificationTypeRegistryService Create()
    {
        return new MockClassificationTypeRegistry();
    }
    
    /// <summary>
    /// Registers a classification type with the given name.
    /// </summary>
    /// <param name="name">The name of the classification type to register.</param>
    private void RegisterType(string name)
    {
        _types[name] = new MockClassificationType(name);
    }
    
    /// <inheritdoc/>
    public IClassificationType GetClassificationType(string type)
    {
        return _types.TryGetValue(type, out var result) ? result : null;
    }
    
    /// <inheritdoc/>
    public IClassificationType CreateClassificationType(string type, IEnumerable<IClassificationType> baseTypes)
    {
        var newType = new MockClassificationType(type);
        _types[type] = newType;
        return newType;
    }
    
    /// <inheritdoc/>
    public ILayeredClassificationType GetClassificationType(ClassificationLayer layer, string type) => null;

    /// <inheritdoc/>
    public ILayeredClassificationType CreateClassificationType(ClassificationLayer layer, string type, IEnumerable<IClassificationType> baseTypes) => null;

    /// <inheritdoc/>
    public IClassificationType CreateTransientClassificationType(IEnumerable<IClassificationType> baseTypes) => throw new NotImplementedException();

    /// <inheritdoc/>
    public IClassificationType CreateTransientClassificationType(params IClassificationType[] baseTypes) => throw new NotImplementedException();
}

/// <summary>
/// Mock implementation of IClassificationType for testing.
/// Represents a specific classification type.
/// </summary>
/// <param name="classification">The name of the classification type.</param>
internal class MockClassificationType(string classification) : IClassificationType
{
    /// <inheritdoc/>
    public string Classification { get; } = classification;

    /// <inheritdoc/>
    public IEnumerable<IClassificationType> BaseTypes { get; } = [];

    /// <inheritdoc/>
    public bool IsOfType(string type) => Classification.Equals(type, StringComparison.OrdinalIgnoreCase);
}