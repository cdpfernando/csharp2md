using Csharp2Md.Analysis.Classification;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class SignatureReaderTests
{
    private const string ContextFqn = "global::Acme.Orders.Data.OrderDbContext";
    private const string DbSetOfOrder = "global::Microsoft.EntityFrameworkCore.DbSet<global::Acme.Orders.Data.Order>";

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Field_EachSignatureComponent_ReturnsTheDecodedValue()
    {
        var signature = CanonicalSymbolSignature.Create("property", ContextFqn, "Orders", 0, DbSetOfOrder).Value;

        Assert.Equal("property", SignatureReader.Field(signature, "kind"));
        Assert.Equal(ContextFqn, SignatureReader.Field(signature, "container"));
        Assert.Equal("Orders", SignatureReader.Field(signature, "metadata"));
        Assert.Equal("0", SignatureReader.Field(signature, "arity"));
        Assert.Equal(DbSetOfOrder, SignatureReader.Field(signature, "type"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Field_OmittedComponent_ReturnsNullForTheSentinel()
    {
        var signature = CanonicalSymbolSignature.Create("property", ContextFqn, "Orders", 0, DbSetOfOrder).Value;

        Assert.Contains(";parameters=-", signature, StringComparison.Ordinal);
        Assert.Null(SignatureReader.Field(signature, "parameters"));
        Assert.Null(SignatureReader.Field(signature, "type-arguments"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Field_EscapedComponent_IsUnescaped()
    {
        var signature = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Orders.Data.OrderWrites",
            "PlaceOrder",
            0,
            "global::System.Threading.Tasks.Task",
            [new SymbolParameterSignature("global::System.Collections.Generic.List<global::System.Guid>")]).Value;

        Assert.DoesNotContain("<", signature, StringComparison.Ordinal);
        Assert.Equal("global::System.Collections.Generic.List<global::System.Guid>", SignatureReader.Field(signature, "parameters"));
        Assert.Equal("global::Acme.Orders.Data.OrderWrites", SignatureReader.Field(signature, "container"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Field_MalformedSignature_ReturnsNull()
    {
        Assert.Null(SignatureReader.Field("not a signature at all", "kind"));
        Assert.Null(SignatureReader.Field("sig1;kind", "kind"));
        Assert.Null(SignatureReader.Field(string.Empty, "container"));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void ConvenienceReaders_ReadTheirComponentFromASymbol()
    {
        var symbol = DbSetProperty();

        Assert.Equal("property", SignatureReader.Kind(symbol));
        Assert.Equal(ContextFqn, SignatureReader.Container(symbol));
        Assert.Equal("Orders", SignatureReader.Metadata(symbol));
        Assert.Equal(DbSetOfOrder, SignatureReader.Type(symbol));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void SoleTypeArgument_DbSetPropertyType_ReturnsTheEntityType()
    {
        Assert.Equal("global::Acme.Orders.Data.Order", SignatureReader.SoleTypeArgument(SignatureReader.Type(DbSetProperty())));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void SoleTypeArgument_NonGenericOrMultiArgumentType_ReturnsNull()
    {
        Assert.Null(SignatureReader.SoleTypeArgument("global::Acme.Orders.Data.Order"));
        Assert.Null(SignatureReader.SoleTypeArgument("global::System.Collections.Generic.Dictionary<global::System.Guid,global::System.String>"));
        Assert.Null(SignatureReader.SoleTypeArgument(null));
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void SoleTypeArgument_NestedGenericArgument_KeepsTheWholeArgument()
    {
        Assert.Equal(
            "global::System.Collections.Generic.List<global::System.Guid>",
            SignatureReader.SoleTypeArgument(
                "global::Microsoft.EntityFrameworkCore.DbSet<global::System.Collections.Generic.List<global::System.Guid>>"));
    }

    private static Symbol DbSetProperty() =>
        Symbol.Create(
            CanonicalSymbolSignature.Create("property", ContextFqn, "Orders", 0, DbSetOfOrder),
            ProjectId.Create(
                SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx"),
                "Acme.Orders/Acme.Orders.csproj"),
            SymbolFacetSet.Create([]));
}
