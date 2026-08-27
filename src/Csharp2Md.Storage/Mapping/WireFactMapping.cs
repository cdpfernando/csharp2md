using System.Globalization;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class WireFactMapping
{
    public static FactReferenceDto ToDto(FactReference reference) =>
        new(reference.Id.Value, reference.FactType);

    public static FactReference FromDto(FactReferenceDto dto) =>
        new(new FactId(FactIdComponents.TypeOf(dto.Id), dto.Id), dto.FactType);

    public static StructuralLiteralDto ToDto(StructuralLiteral literal) =>
        new(literal.Value, literal.Role.ToString());

    public static StructuralLiteral FromLiteral(StructuralLiteralDto dto, string fieldName) =>
        StructuralLiteral.Create(Enum.Parse<LiteralRole>(dto.Role), dto.Value, fieldName);

    public static SolutionId SolutionIdFromValue(string value)
    {
        var components = FactIdComponents.Parse(value);
        var workspace = FactIdComponents.Parse(components["workspace"]);
        return SolutionId.Create(WorkspaceIdentity.Create(workspace["name"]), components["path"]);
    }

    public static ProjectId ProjectIdFromValue(string value)
    {
        var components = FactIdComponents.Parse(value);
        var solution = SolutionIdFromValue(components["solution"]);
        return ProjectId.Create(solution, components["path"]);
    }

    public static CanonicalSymbolSignature SignatureFromValue(string value)
    {
        var components = FactIdComponents.Parse(value);
        IEnumerable<SymbolParameterSignature>? parameters = null;
        if (components.TryGetValue("parameters", out var parameterText) && parameterText.Length > 0)
        {
            parameters = SplitTopLevel(parameterText).Select(ParseParameter);
        }

        IEnumerable<string>? typeArguments = null;
        if (components.TryGetValue("type-arguments", out var typeArgumentText) && typeArgumentText.Length > 0)
        {
            typeArguments = SplitTopLevel(typeArgumentText);
        }

        return CanonicalSymbolSignature.Create(
            components["kind"],
            components["container"],
            components["metadata"],
            int.Parse(components["arity"], CultureInfo.InvariantCulture),
            components["type"],
            parameters,
            typeArguments);
    }

    public static TEnum ParseWire<TEnum>(string wire)
        where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
        {
            if (string.Equals(FacetAxes.WireValue(value), wire, StringComparison.Ordinal))
            {
                return value;
            }
        }

        throw new InvalidOperationException($"'{wire}' is not a registered wire value of '{typeof(TEnum).Name}'.");
    }

    public static SolutionDto ToDto(Solution fact) =>
        PayloadHash.Attach(
            new SolutionDto(ToDto(fact.Reference), fact.Id.Value, string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static ProjectDto ToDto(Project fact) =>
        PayloadHash.Attach(
            new ProjectDto(ToDto(fact.Reference), fact.Id.Value, string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static DocumentDto ToDto(Document fact)
    {
        var dto = new DocumentDto(ToDto(fact.Reference), fact.OwningProject.Value, fact.RelativePath, string.Empty);
        if (fact.SourceHash is { } hash)
        {
            return dto with { ContentSha256 = hash.Value };
        }

        return PayloadHash.Attach(dto, static (candidate, digest) => candidate with { ContentSha256 = digest });
    }

    public static SymbolDto ToDto(Symbol fact) =>
        PayloadHash.Attach(
            new SymbolDto(
                ToDto(fact.Reference),
                fact.OwningProject.Value,
                fact.Signature.Value,
                [.. fact.Facets.Facets.Select(FacetAxes.WireValue)],
                fact.DeclarationLocator is { } locator ? ToDto(locator) : null,
                string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static ComponentDto ToDto(Component fact) =>
        PayloadHash.Attach(
            new ComponentDto(ToDto(fact.Reference), fact.Solution.Value, fact.Name, [.. fact.Owners.Select(ToDto)], string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static DeploymentUnitDto ToDto(DeploymentUnit fact) =>
        PayloadHash.Attach(
            new DeploymentUnitDto(ToDto(fact.Reference), fact.Solution.Value, fact.Name, string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static EntryPointDto ToDto(EntryPoint fact) =>
        PayloadHash.Attach(
            new EntryPointDto(ToDto(fact.Reference), ToDto(fact.Symbol), ToDto(fact.OwningComponent), string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static BoundaryOperationDto ToDto(BoundaryOperation fact) =>
        PayloadHash.Attach(
            new BoundaryOperationDto(
                ToDto(fact.Reference),
                ToDto(fact.Symbol),
                ToDto(fact.OwningComponent),
                FacetAxes.WireValue(fact.Direction),
                fact.Protocol is { } protocol ? FacetAxes.WireValue(protocol) : null,
                fact.DestinationScope,
                fact.HttpMethod,
                fact.Route is { } route ? ToDto(route) : null,
                fact.ProtocolOperationKey is { } key ? ToDto(key) : null,
                string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static ExternalSystemDto ToDto(ExternalSystem fact) =>
        PayloadHash.Attach(
            new ExternalSystemDto(ToDto(fact.Reference), fact.Solution.Value, ToDto(fact.Name), string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static ContractDto ToDto(Contract fact) =>
        PayloadHash.Attach(
            new ContractDto(ToDto(fact.Reference), ToDto(fact.Proof), string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static ContractBindingDto ToDto(ContractBinding fact) =>
        PayloadHash.Attach(
            new ContractBindingDto(ToDto(fact.Reference), ToDto(fact.Operation), fact.PayloadRole, ToDto(fact.ClrSymbol), ToDto(fact.Contract), string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static ContractRevisionDto ToDto(ContractRevision fact) =>
        PayloadHash.Attach(
            new ContractRevisionDto(ToDto(fact.Reference), ToDto(fact.Contract), fact.StructuralFingerprint, string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static DataStoreDto ToDto(DataStore fact) =>
        PayloadHash.Attach(
            new DataStoreDto(ToDto(fact.Reference), FacetAxes.WireValue(fact.Technology), ToDto(fact.Name), string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static DataObjectDto ToDto(DataObject fact) =>
        PayloadHash.Attach(
            new DataObjectDto(
                ToDto(fact.Reference),
                ToDto(fact.Store),
                FacetAxes.WireValue(fact.Form),
                ToDto(fact.SchemaName),
                ToDto(fact.TableName),
                FacetAxes.WireValue(fact.MappingState),
                string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static DataFieldDto ToDto(DataField fact) =>
        PayloadHash.Attach(
            new DataFieldDto(
                ToDto(fact.Reference),
                ToDto(fact.DataObject),
                ToDto(fact.FieldName),
                FacetAxes.WireValue(fact.MappingState),
                string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static DataOperationDto ToDto(DataOperation fact) =>
        PayloadHash.Attach(
            new DataOperationDto(
                ToDto(fact.Reference),
                ToDto(fact.Target),
                FacetAxes.WireValue(fact.Operation),
                FacetAxes.WireValue(fact.MappingState),
                string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static ConfigurationBindingDto ToDto(ConfigurationBinding fact) =>
        PayloadHash.Attach(
            new ConfigurationBindingDto(ToDto(fact.Reference), ToDto(fact.BoundFact), ToDto(fact.ConfigurationKey), string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static Solution FromDto(SolutionDto dto) =>
        Solution.Create(SolutionIdFromValue(dto.SolutionId));

    public static Project FromDto(ProjectDto dto) =>
        Project.Create(ProjectIdFromValue(dto.ProjectId));

    public static Document FromDto(DocumentDto dto)
    {
        var payload = CanonicalJson.PayloadContentSha256(dto);
        DocumentHash? sourceHash = null;
        if (!string.Equals(dto.ContentSha256, payload, StringComparison.Ordinal))
        {
            sourceHash = DocumentHash.Create(dto.ContentSha256);
        }

        return Document.Create(ProjectIdFromValue(dto.OwningProject), dto.RelativePath, sourceHash);
    }

    public static Symbol FromDto(SymbolDto dto) =>
        Symbol.Create(
            SignatureFromValue(dto.CanonicalSymbolSignature),
            ProjectIdFromValue(dto.OwningProject),
            SymbolFacetSet.Create(dto.Facets.Select(ParseWire<SymbolFacet>)),
            dto.DeclarationLocator is { } locator ? FromDto(locator) : null);

    private static DeclarationLocatorDto ToDto(DeclarationLocator locator) =>
        new(
            locator.Document.Value,
            locator.RelativePath,
            new SourceSpanDto(locator.Span.StartLine, locator.Span.StartColumn, locator.Span.EndLine, locator.Span.EndColumn),
            locator.Hash.Value);

    private static DeclarationLocator FromDto(DeclarationLocatorDto dto) =>
        new(
            DocumentId.Create(dto.Document),
            dto.RelativePath,
            new SourceSpan(dto.Span.StartLine, dto.Span.StartColumn, dto.Span.EndLine, dto.Span.EndColumn),
            DocumentHash.Create(dto.Hash));

    public static Component FromDto(ComponentDto dto) =>
        Component.Create(SolutionIdFromValue(dto.Solution), dto.Name, dto.Owners.Select(FromDto));

    public static DeploymentUnit FromDto(DeploymentUnitDto dto) =>
        DeploymentUnit.Create(SolutionIdFromValue(dto.Solution), dto.Name);

    public static EntryPoint FromDto(EntryPointDto dto) =>
        EntryPoint.Create(FromDto(dto.Symbol), FromDto(dto.OwningComponent));

    public static BoundaryOperation FromDto(BoundaryOperationDto dto) =>
        BoundaryOperation.Create(
            FromDto(dto.Symbol),
            FromDto(dto.OwningComponent),
            ParseWire<BoundaryDirection>(dto.Direction),
            dto.Protocol is null ? null : ParseWire<BoundaryProtocol>(dto.Protocol),
            dto.DestinationScope,
            dto.HttpMethod,
            dto.Route is null ? null : FromLiteral(dto.Route, "route"),
            dto.ProtocolOperationKey is null ? null : FromLiteral(dto.ProtocolOperationKey, "protocolOperationKey"));

    public static ExternalSystem FromDto(ExternalSystemDto dto) =>
        ExternalSystem.Create(SolutionIdFromValue(dto.Solution), FromLiteral(dto.Name, "name"));

    public static Contract FromDto(ContractDto dto) =>
        Contract.Create(FromLiteral(dto.Proof, "proof"));

    public static ContractBinding FromDto(ContractBindingDto dto) =>
        ContractBinding.Create(FromDto(dto.Operation), dto.PayloadRole, FromDto(dto.ClrSymbol), FromDto(dto.Contract));

    public static ContractRevision FromDto(ContractRevisionDto dto) =>
        ContractRevision.Create(FromDto(dto.Contract), dto.StructuralFingerprint);

    public static DataStore FromDto(DataStoreDto dto) =>
        DataStore.Create(ParseWire<DataStoreTechnology>(dto.Technology), FromLiteral(dto.Name, "name"));

    public static DataObject FromDto(DataObjectDto dto) =>
        DataObject.Create(
            FromDto(dto.Store),
            ParseWire<DataObjectForm>(dto.Form),
            FromLiteral(dto.SchemaName, "schemaName"),
            FromLiteral(dto.TableName, "tableName"),
            ParseWire<MappingStateKind>(dto.MappingState));

    public static DataField FromDto(DataFieldDto dto) =>
        DataField.Create(
            FromDto(dto.DataObject),
            FromLiteral(dto.FieldName, "fieldName"),
            ParseWire<MappingStateKind>(dto.MappingState));

    public static DataOperation FromDto(DataOperationDto dto) =>
        DataOperation.Create(
            FromDto(dto.Target),
            ParseWire<DataOperationKind>(dto.Operation),
            ParseWire<MappingStateKind>(dto.MappingState));

    public static ConfigurationBinding FromDto(ConfigurationBindingDto dto) =>
        ConfigurationBinding.Create(FromDto(dto.BoundFact), FromLiteral(dto.ConfigurationKey, "configurationKey"));

    private static IEnumerable<string> SplitTopLevel(string text)
    {
        var start = 0;
        var depth = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            switch (current)
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    if (depth > 0)
                    {
                        depth--;
                    }

                    break;
                case ',' when depth == 0:
                    yield return text[start..index];
                    start = index + 1;
                    break;
            }
        }

        yield return text[start..];
    }

    private static SymbolParameterSignature ParseParameter(string text)
    {
        if (text.StartsWith("ref ", StringComparison.Ordinal))
        {
            return new SymbolParameterSignature(text[4..], SymbolParameterModifier.Ref);
        }

        if (text.StartsWith("out ", StringComparison.Ordinal))
        {
            return new SymbolParameterSignature(text[4..], SymbolParameterModifier.Out);
        }

        if (text.StartsWith("in ", StringComparison.Ordinal))
        {
            return new SymbolParameterSignature(text[3..], SymbolParameterModifier.In);
        }

        return new SymbolParameterSignature(text);
    }
}
