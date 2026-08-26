using Csharp2Md.Analysis.Classification;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class PayloadReaderTests
{
    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Value_PresentEntry_ReturnsStoredLiteral()
    {
        var observation = Observe(Entry("context-type", "global::Acme.Orders.Data.OrderDbContext"));

        Assert.Equal("global::Acme.Orders.Data.OrderDbContext", PayloadReader.Value(observation, "context-type"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Value_AbsentEntry_ReturnsNull()
    {
        var observation = Observe(Entry("context-type", "global::Acme.Orders.Data.OrderDbContext"));

        Assert.Null(PayloadReader.Value(observation, "entity-type"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Value_BlankEntry_ReturnsNull()
    {
        var observation = Observe(new PayloadEntry("entity-type", default));

        Assert.Null(PayloadReader.Value(observation, "entity-type"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Contains_EntryHoldingNeedle_ReturnsTrue()
    {
        var observation = Observe(Entry("field-names", "Amount|Id|Status"));

        Assert.True(PayloadReader.Contains(observation, "field-names", "Status"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Contains_EntryWithoutNeedle_ReturnsFalse()
    {
        var observation = Observe(Entry("field-names", "Amount|Id"));

        Assert.False(PayloadReader.Contains(observation, "field-names", "Status"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Contains_AbsentEntry_ReturnsFalse()
    {
        var observation = Observe(Entry("field-names", "Status"));

        Assert.False(PayloadReader.Contains(observation, "sql-columns", "Status"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Multi_JoinedEntry_ReturnsEveryName()
    {
        var observation = Observe(Entry("sql-columns", "Amount|Id|Status"));

        Assert.Equal<string>(["Amount", "Id", "Status"], PayloadReader.Multi(observation, "sql-columns"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Multi_SingleValuedEntry_ReturnsThatOneName()
    {
        var observation = Observe(Entry("sql-columns", "Status"));

        Assert.Equal<string>(["Status"], PayloadReader.Multi(observation, "sql-columns"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Multi_AbsentEntry_ReturnsEmpty()
    {
        var observation = Observe(Entry("sql-columns", "Status"));

        Assert.Empty(PayloadReader.Multi(observation, "field-names"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Multi_BlankEntry_ReturnsEmpty()
    {
        var observation = Observe(new PayloadEntry("sql-columns", default));

        Assert.Empty(PayloadReader.Multi(observation, "sql-columns"));
    }

    private static PayloadEntry Entry(string key, string value) =>
        new(key, StructuralLiteral.Create(LiteralRole.FieldName, value, key));

    private static Observation Observe(params PayloadEntry[] entries) =>
        Observation.Create(
            Symbol.Create(
                CanonicalSymbolSignature.Create("method", "global::Acme.Orders.OrderWrites", "PlaceOrder", 0, "global::System.Void"),
                ProjectId.Create(
                    SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx"),
                    "Acme.Orders/Acme.Orders.csproj"),
                SymbolFacetSet.Create([SymbolFacet.Callable])).Reference,
            ObservationKind.DataAccess,
            NormalizedPayload.Create(entries),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "Acme.Orders/Data/OrderWrites.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
