using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class SymbolLocatorRoundTripTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln"),
        "src/Acme.Payments/Acme.Payments.csproj");

    private static CanonicalSymbolSignature RunSignature => CanonicalSymbolSignature.Create(
        "method", "global::Acme.Payment", "Run", 0, "global::System.Void");

    private static DeclarationLocator Locator => new(
        DocumentId.Create("src/Acme.Payments/Invoice.cs"),
        "src/Acme.Payments/Invoice.cs",
        new SourceSpan(10, 1, 24, 2),
        DocumentHash.Create(new string('a', 64)));

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void RoundTrip_SymbolWithLocator_RestoresEqualLocator()
    {
        var original = Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable]), Locator);
        var restored = DomainMapper.FromWire(DomainMapper.ToWire(Snapshot(original), Context));

        var symbol = Assert.IsType<Symbol>(Assert.Single(restored.Facts));
        Assert.Equal(Locator, symbol.DeclarationLocator);
        Assert.Equal(original.Reference.Id.Value, symbol.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void RoundTrip_SymbolWithoutLocator_RestoresAbsentLocator()
    {
        var original = Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable]));
        var restored = DomainMapper.FromWire(DomainMapper.ToWire(Snapshot(original), Context));

        var symbol = Assert.IsType<Symbol>(Assert.Single(restored.Facts));
        Assert.Null(symbol.DeclarationLocator);
        Assert.Null(original.DeclarationLocator);
    }

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void ToDto_SymbolWithoutLocator_LeavesDeclarationLocatorNullNotDefault()
    {
        var dto = WireFactMapping.ToDto(
            Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable])));

        Assert.Null(dto.DeclarationLocator);
    }

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void ToDto_SymbolWithLocator_CarriesDocumentSpanAndHash()
    {
        var dto = WireFactMapping.ToDto(
            Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable]), Locator));

        Assert.NotNull(dto.DeclarationLocator);
        var locator = dto.DeclarationLocator;
        Assert.Equal(Locator.Document.Value, locator.Document);
        Assert.Equal(Locator.RelativePath, locator.RelativePath);
        Assert.Equal(Locator.Span.StartLine, locator.Span.StartLine);
        Assert.Equal(Locator.Span.StartColumn, locator.Span.StartColumn);
        Assert.Equal(Locator.Span.EndLine, locator.Span.EndLine);
        Assert.Equal(Locator.Span.EndColumn, locator.Span.EndColumn);
        Assert.Equal(Locator.Hash.Value, locator.Hash);
    }

    private static FactualSnapshot Snapshot(IFact fact) => new([fact], [], [], [], [], []);
}
