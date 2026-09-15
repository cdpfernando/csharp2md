using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class NestedSignatureRoundTripTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln"),
        "src/Acme.Payments/Acme.Payments.csproj");

    public static TheoryData<string, CanonicalSymbolSignature> NestedSignatures() => new()
    {
        {
            "named-tuple",
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Payment",
                "Run",
                0,
                "global::System.Void",
                [new("(global::System.String Name, global::System.Int32 Age)")])
        },
        {
            "two-tuples",
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Payment",
                "Run",
                0,
                "global::System.Void",
                [
                    new("(global::System.String Name, global::System.Int32 Age)"),
                    new("(global::System.Int32 X, global::System.Int32 Y)"),
                ])
        },
        {
            "nested-generic-tuple",
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Payment",
                "Run",
                0,
                "global::System.Void",
                [
                    new("global::System.Collections.Generic.Dictionary<(global::System.String, global::System.Int32), global::System.Int32>"),
                ])
        },
        {
            "multidimensional-array",
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Payment",
                "Run",
                0,
                "global::System.Void",
                [new("global::System.Int32[,,]")])
        },
        {
            "mixed",
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Payment",
                "Run",
                1,
                "global::System.Void",
                [
                    new("global::System.Collections.Generic.List<(global::System.String Name, global::System.Int32 Age)>"),
                    new("global::System.Int32[,,]"),
                    new("(global::System.Boolean Ok, global::System.Byte[] Buffer)"),
                ],
                ["global::System.Collections.Generic.Dictionary<(global::System.String, global::System.Int32), global::System.Int32>"])
        },
    };

    [Theory]
    [Trait("Requirement", "APR-16")]
    [Trait("Requirement", "APR-17")]
    [Trait("Requirement", "APR-18")]
    [Trait("Requirement", "APR-19")]
    [Trait("Requirement", "APR-20")]
    [MemberData(nameof(NestedSignatures))]
    public void RoundTrip_NestedSignature_RestoresExactCanonicalEquality(
        string shape,
        CanonicalSymbolSignature signature)
    {
        Assert.False(string.IsNullOrWhiteSpace(shape));
        var symbol = Symbol.Create(signature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable]));
        var snapshot = new FactualSnapshot([symbol], [], [], [], [], []);

        var restored = DomainMapper.FromWire(DomainMapper.ToWire(snapshot, Context));
        var restoredSymbol = Assert.Single(restored.Facts.OfType<Symbol>());

        Assert.Equal(symbol, restoredSymbol);
        Assert.Equal(signature, restoredSymbol.Signature);
        Assert.Equal(signature.Value, restoredSymbol.Signature.Value);
    }

    [Fact]
    [Trait("Requirement", "APR-21")]
    public void RoundTrip_SimpleSymbol_IsByteIdenticalOnTheWire()
    {
        var signature = CanonicalSymbolSignature.Create(
            "method", "global::Acme.Payment", "Run", 0, "global::System.Void");
        var snapshot = new FactualSnapshot(
            [Symbol.Create(signature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable]))],
            [],
            [],
            [],
            [],
            []);
        var firstDto = Assert.Single(DomainMapper.ToWire(snapshot, Context).Symbols);
        var restored = DomainMapper.FromWire(DomainMapper.ToWire(snapshot, Context));
        var secondDto = Assert.Single(
            DomainMapper.ToWire(new FactualSnapshot([restored.Facts[0]], [], [], [], [], []), Context).Symbols);

        Assert.Equal(snapshot.Facts[0], restored.Facts[0]);
        Assert.Equal(firstDto.CanonicalSymbolSignature, secondDto.CanonicalSymbolSignature);
        Assert.True(CanonicalJson.Write(firstDto).AsSpan().SequenceEqual(CanonicalJson.Write(secondDto).AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "APR-22")]
    public void Validate_UnbalancedWireSignature_RejectsConstructionAndIsNotStored()
    {
        var signature = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payment",
            "Run",
            0,
            "global::System.Void",
            [new("global::System.Collections.Generic.List<global::System.Int32>")]);
        var snapshot = new FactualSnapshot(
            [Symbol.Create(signature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable]))],
            [],
            [],
            [],
            [],
            []);
        var document = DomainMapper.ToWire(snapshot, Context);
        var original = Assert.Single(document.Symbols);
        Assert.Contains("%3E", original.CanonicalSymbolSignature, StringComparison.Ordinal);
        var invalid = original with
        {
            CanonicalSymbolSignature = original.CanonicalSymbolSignature.Replace("%3E", string.Empty, StringComparison.Ordinal),
        };
        invalid = invalid with { ContentSha256 = CanonicalJson.PayloadContentSha256(invalid) };
        var mutated = document with { Symbols = [invalid] };

        var exception = Assert.Throws<PublicationRejectedException>(() => PackageValidator.Validate(mutated));

        Assert.Equal("construction", exception.Gate);
        Assert.Contains(invalid.Identity.Id, exception.Detail, StringComparison.Ordinal);
        Assert.Throws<PublicationRejectedException>(() => PackageValidator.Validate(mutated));
    }
}
