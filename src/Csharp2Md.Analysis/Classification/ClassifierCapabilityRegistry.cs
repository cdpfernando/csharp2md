namespace Csharp2Md.Analysis.Classification;

/// <summary>
/// Declared by a classifier pass that consumes raw document content directly, by extension, rather
/// than only Roslyn-derived facts and observations. The extensions a registered pass declares here
/// form the conditionally supported set in <c>SupportedDocumentPolicy</c> (GCPC-029): an extension is
/// admitted only when an active pass declares that it consumes it.
/// </summary>
internal interface IDocumentConsumingClassifierPass
{
    ImmutableArray<string> ConsumedDocumentExtensions { get; }
}

/// <summary>
/// Derives the set of document extensions the currently active classifier passes consume, so the
/// conditionally supported document set is computed from what is actually registered rather than
/// hand-maintained (GCPC-029).
/// </summary>
internal sealed class ClassifierCapabilityRegistry
{
    private readonly ImmutableArray<IClassifierPass> _passes;

    public ClassifierCapabilityRegistry(ImmutableArray<IClassifierPass> passes) =>
        _passes = passes.IsDefault ? ImmutableArray<IClassifierPass>.Empty : passes;

    public ImmutableArray<string> ConsumedExtensions() =>
        _passes
            .OfType<IDocumentConsumingClassifierPass>()
            .SelectMany(static pass => pass.ConsumedDocumentExtensions)
            .Select(NormalizeExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

    private static string NormalizeExtension(string extension) =>
        extension.StartsWith('.') ? extension : "." + extension;
}
