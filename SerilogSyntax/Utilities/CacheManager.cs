using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Parsing;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SerilogSyntax.Utilities;

/// <summary>
/// Manages caching for the Serilog classifier to improve performance.
/// </summary>
internal class CacheManager
{
    private readonly ConcurrentDictionary<string, List<TemplateProperty>> _templateCache = new();
    private readonly ConcurrentDictionary<SnapshotSpan, List<ClassificationSpan>> _classificationCache = new();
    private readonly object _cacheLock = new();
    private readonly TemplateParser _parser;

    public CacheManager(TemplateParser parser)
    {
        _parser = parser;
    }

    /// <summary>
    /// Gets parsed template properties from cache or parses and caches them.
    /// </summary>
    /// <param name="template">The template string to parse.</param>
    /// <returns>List of template properties found in the template.</returns>
    public List<TemplateProperty> GetCachedTemplateProperties(string template)
    {
        // Use template cache to avoid re-parsing identical templates
        return _templateCache.GetOrAdd(template, t =>
        {
            try
            {
                return [.. _parser.Parse(t)];
            }
            catch
            {
                // Return empty list on parse error
                return [];
            }
        });
    }

    /// <summary>
    /// Tries to get cached classification spans for a given span.
    /// </summary>
    /// <param name="span">The span to look up.</param>
    /// <param name="classifications">The cached classifications if found.</param>
    /// <returns>True if cached classifications were found; otherwise, false.</returns>
    public bool TryGetCachedClassifications(SnapshotSpan span, out List<ClassificationSpan> classifications)
    {
        return _classificationCache.TryGetValue(span, out classifications);
    }

    /// <summary>
    /// Adds classification spans to the cache for a given span.
    /// </summary>
    /// <param name="span">The span to cache classifications for.</param>
    /// <param name="classifications">The classifications to cache.</param>
    public void CacheClassifications(SnapshotSpan span, List<ClassificationSpan> classifications)
    {
        _classificationCache.TryAdd(span, classifications);
    }

    /// <summary>
    /// Invalidates cache entries that overlap with the given spans.
    /// </summary>
    /// <param name="spans">The spans to invalidate cache for.</param>
    public void InvalidateCacheForSpans(List<SnapshotSpan> spans)
    {
        var keysToRemove = new List<SnapshotSpan>();

        lock (_cacheLock)
        {
            foreach (var cachedSpan in _classificationCache.Keys)
            {
                foreach (var changedSpan in spans)
                {
                    if (cachedSpan.OverlapsWith(changedSpan))
                    {
                        keysToRemove.Add(cachedSpan);
                        break;
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _classificationCache.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Clears all caches.
    /// </summary>
    public void Clear()
    {
        _templateCache.Clear();
        _classificationCache.Clear();
    }
}