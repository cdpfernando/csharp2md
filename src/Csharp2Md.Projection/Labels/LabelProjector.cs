using Csharp2Md.Projection.Source;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Labels;

/// <summary>
/// Compact labels for the component, type, method, protocol, verb and route values proven for one
/// identity (GCPC-093), each derived from a value present in the authoritative payload and cited by the
/// artifact key and ordinal it came from (GCPC-094). The canonical identity stays the authority: a label
/// is never inferred for a value that is not proven (GCPC-098), and a value that traces back to a
/// symbol's declaration inside a declared redacted span is omitted rather than published (GCPC-084).
/// </summary>
internal static class LabelProjector
{
    internal const string Component = "component";
    internal const string Type = "type";
    internal const string Method = "method";
    internal const string Protocol = "protocol";
    internal const string Verb = "verb";
    internal const string Route = "route";

    /// <summary>A plain self-name label for identities (contracts, data stores, ...) that carry none of
    /// the six axes above but would otherwise title a page by nothing but fact type plus an encoded id
    /// (GCPC-096).</summary>
    internal const string Name = "name";

    public static ImmutableArray<LabelDto> For(FactReferenceDto identity, PublishedPackageView view)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(view);

        return identity.FactType switch
        {
            "EntryPoint" => ForEntryPoint(identity, view),
            "BoundaryOperation" => ForBoundaryOperation(identity, view),
            "Component" => ForComponent(identity, view),
            "DeploymentUnit" => ForDeploymentUnit(identity, view),
            "Contract" => ForContract(identity, view),
            "DataStore" => ForDataStore(identity, view),
            "DataObject" => ForDataObject(identity, view),
            _ => [],
        };
    }

    private static ImmutableArray<LabelDto> ForEntryPoint(FactReferenceDto identity, PublishedPackageView view)
    {
        var entryPoint = view.Document.EntryPoints.FirstOrDefault(dto => dto.Identity.Id == identity.Id);
        if (entryPoint is null)
        {
            return [];
        }

        var labels = ImmutableArray.CreateBuilder<LabelDto>();
        AddComponentLabel(labels, entryPoint.OwningComponent.Id, view);
        AddSymbolLabels(labels, entryPoint.Symbol.Id, view);
        return labels.ToImmutable();
    }

    private static ImmutableArray<LabelDto> ForBoundaryOperation(FactReferenceDto identity, PublishedPackageView view)
    {
        var operation = view.Document.BoundaryOperations.FirstOrDefault(dto => dto.Identity.Id == identity.Id);
        if (operation is null || !view.TryLocate(identity.Id, out var self))
        {
            return [];
        }

        var labels = ImmutableArray.CreateBuilder<LabelDto>();
        AddComponentLabel(labels, operation.OwningComponent.Id, view);
        AddSymbolLabels(labels, operation.Symbol.Id, view);

        if (!string.IsNullOrEmpty(operation.Protocol))
        {
            labels.Add(new LabelDto(Protocol, operation.Protocol, self.ArtifactKey, self.Ordinal));
        }

        if (!string.IsNullOrEmpty(operation.HttpMethod))
        {
            labels.Add(new LabelDto(Verb, operation.HttpMethod, self.ArtifactKey, self.Ordinal));
        }

        if (operation.Route is { Value.Length: > 0 } route)
        {
            labels.Add(new LabelDto(Route, route.Value, self.ArtifactKey, self.Ordinal));
        }

        return labels.ToImmutable();
    }

    private static ImmutableArray<LabelDto> ForComponent(FactReferenceDto identity, PublishedPackageView view)
    {
        var component = view.Document.Components.FirstOrDefault(dto => dto.Identity.Id == identity.Id);
        if (component is null || string.IsNullOrEmpty(component.Name) || !view.TryLocate(identity.Id, out var self))
        {
            return [];
        }

        return [new LabelDto(Component, component.Name, self.ArtifactKey, self.Ordinal)];
    }

    private static ImmutableArray<LabelDto> ForDeploymentUnit(FactReferenceDto identity, PublishedPackageView view)
    {
        var unit = view.Document.DeploymentUnits.FirstOrDefault(dto => dto.Identity.Id == identity.Id);
        if (unit is null || string.IsNullOrEmpty(unit.Name) || !view.TryLocate(identity.Id, out var self))
        {
            return [];
        }

        return [new LabelDto(Name, unit.Name, self.ArtifactKey, self.Ordinal)];
    }

    private static ImmutableArray<LabelDto> ForContract(FactReferenceDto identity, PublishedPackageView view)
    {
        var contract = view.Document.Contracts.FirstOrDefault(dto => dto.Identity.Id == identity.Id);
        if (contract is null || string.IsNullOrEmpty(contract.Proof.Value) || !view.TryLocate(identity.Id, out var self))
        {
            return [];
        }

        return [new LabelDto(Name, contract.Proof.Value, self.ArtifactKey, self.Ordinal)];
    }

    private static ImmutableArray<LabelDto> ForDataStore(FactReferenceDto identity, PublishedPackageView view)
    {
        var store = view.Document.DataStores.FirstOrDefault(dto => dto.Identity.Id == identity.Id);
        if (store is null || string.IsNullOrEmpty(store.Name.Value) || !view.TryLocate(identity.Id, out var self))
        {
            return [];
        }

        return [new LabelDto(Name, store.Name.Value, self.ArtifactKey, self.Ordinal)];
    }

    private static ImmutableArray<LabelDto> ForDataObject(FactReferenceDto identity, PublishedPackageView view)
    {
        var dataObject = view.Document.DataObjects.FirstOrDefault(dto => dto.Identity.Id == identity.Id);
        if (dataObject is null || !view.TryLocate(identity.Id, out var self))
        {
            return [];
        }

        var value = dataObject.TableName.Value.Length > 0 ? dataObject.TableName.Value : dataObject.SchemaName.Value;
        return string.IsNullOrEmpty(value) ? [] : [new LabelDto(Name, value, self.ArtifactKey, self.Ordinal)];
    }

    private static void AddComponentLabel(ImmutableArray<LabelDto>.Builder labels, string componentId, PublishedPackageView view)
    {
        var component = view.Document.Components.FirstOrDefault(dto => dto.Identity.Id == componentId);
        if (component is null || string.IsNullOrEmpty(component.Name) || !view.TryLocate(componentId, out var citation))
        {
            return;
        }

        labels.Add(new LabelDto(Component, component.Name, citation.ArtifactKey, citation.Ordinal));
    }

    private static void AddSymbolLabels(ImmutableArray<LabelDto>.Builder labels, string symbolId, PublishedPackageView view)
    {
        var symbol = view.Document.Symbols.FirstOrDefault(dto => dto.Identity.Id == symbolId);
        if (symbol is null || !view.TryLocate(symbolId, out var citation) || IsRedacted(symbol, view))
        {
            return;
        }

        var type = SignatureField(symbol.CanonicalSymbolSignature, "container");
        var method = SignatureField(symbol.CanonicalSymbolSignature, "metadata");
        if (!string.IsNullOrEmpty(type))
        {
            labels.Add(new LabelDto(Type, type, citation.ArtifactKey, citation.Ordinal));
        }

        if (!string.IsNullOrEmpty(method))
        {
            labels.Add(new LabelDto(Method, method, citation.ArtifactKey, citation.Ordinal));
        }
    }

    /// <summary>
    /// True when the symbol's own declaration lies inside a span this document's diagnostics have
    /// already declared a suspected secret (the same spans <see cref="SourceProjector"/> redacts from
    /// the source projection) -- proven without ever reading the document's bytes, since the spans are
    /// carried as line:column coordinates on the diagnostic itself (GCPC-084).
    /// </summary>
    private static bool IsRedacted(SymbolDto symbol, PublishedPackageView view)
    {
        var locator = symbol.DeclarationLocator;
        if (locator is null)
        {
            return false;
        }

        var spans = SecretRedactor.SpansFor(locator.Document, view.Document.Diagnostics);
        return !spans.IsDefaultOrEmpty && spans.Any(span => Overlaps(span, locator.Span));
    }

    private static bool Overlaps(SourceSpanDto a, SourceSpanDto b) =>
        ComparePosition(a.StartLine, a.StartColumn, b.EndLine, b.EndColumn) <= 0
        && ComparePosition(b.StartLine, b.StartColumn, a.EndLine, a.EndColumn) <= 0;

    private static int ComparePosition(int line1, int column1, int line2, int column2) =>
        line1 != line2 ? line1.CompareTo(line2) : column1.CompareTo(column2);

    /// <summary>
    /// Decodes one field of a <see cref="Csharp2Md.Domain.Identity.CanonicalSymbolSignature"/> without a
    /// cross-assembly dependency on Analysis's own private copy (see
    /// <c>Csharp2Md.Analysis.Classification.SignatureReader</c>, whose own remarks record that every
    /// consumer keeps this reader rather than sharing it).
    /// </summary>
    private static string? SignatureField(string signature, string key)
    {
        var marker = ";" + key + "=";
        var start = signature.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = signature.IndexOf(';', start);
        var encoded = end < 0 ? signature[start..] : signature[start..end];
        return encoded.Length == 0 || encoded == "-" ? null : Uri.UnescapeDataString(encoded);
    }
}
