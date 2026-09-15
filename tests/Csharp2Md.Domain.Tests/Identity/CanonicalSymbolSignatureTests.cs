using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Tests.Identity;

public sealed class CanonicalSymbolSignatureTests
{
    [Fact]
    [Trait("Requirement", "TAX-69")]
    public void Create_AllComponentsParticipateAndParameterNamesDoNot()
    {
        var signature = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payment",
            "TryRun`1",
            1,
            "global::System.Boolean",
            [new("global::System.String", SymbolParameterModifier.Ref), new("global::System.Int32", SymbolParameterModifier.Out)],
            ["global::T"]);

        Assert.Equal(
            "sig1;kind=method;container=global%3A%3AAcme.Payment;metadata=TryRun%601;arity=1;type=global%3A%3ASystem.Boolean;parameters=ref%20global%3A%3ASystem.String%2Cout%20global%3A%3ASystem.Int32;type-arguments=global%3A%3AT",
            signature.Value);
        Assert.DoesNotContain("parameterName", signature.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-76")]
    public void Create_AbsentParametersAndTypeArguments_RenderAsSentinel()
    {
        var signature = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payment",
            "Run",
            0,
            "global::System.Void");

        Assert.EndsWith(";parameters=-;type-arguments=-", signature.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-69")]
    public void Create_NegativeArity_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalSymbolSignature.Create(
            "method", "global::Acme.Payment", "Run", -1, "global::System.Void"));

    [Fact]
    [Trait("Requirement", "TAX-69")]
    public void Create_UnrelatedPrecedingEdit_ProducesByteIdenticalSignatureWithNoLineOrSpanComponent()
    {
        var beforeEdit = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payment",
            "Run`1",
            1,
            "global::System.Threading.Tasks.Task",
            [new("global::System.String", SymbolParameterModifier.In)],
            ["global::T"]);
        var afterEdit = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payment",
            "Run`1",
            1,
            "global::System.Threading.Tasks.Task",
            [new("global::System.String", SymbolParameterModifier.In)],
            ["global::T"]);

        Assert.Equal(beforeEdit, afterEdit);
        Assert.DoesNotContain("line", beforeEdit.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("span", beforeEdit.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UninitializedSignature_Value_ThrowsInvalidOperationException()
    {
        var uninitialized = default(CanonicalSymbolSignature);

        Assert.Throws<InvalidOperationException>(() => uninitialized.Value);
    }

    [Fact]
    [Trait("Requirement", "TAX-69")]
    public void Component_RoundTripsEveryValueCreateEncoded()
    {
        var signature = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payment+Inner",
            "TryRun`1",
            1,
            "global::System.Threading.Tasks.Task<global::System.Boolean>",
            [new("global::System.String", SymbolParameterModifier.Ref)],
            ["global::T"]);

        Assert.Equal("method", signature.Component("kind"));
        Assert.Equal("global::Acme.Payment+Inner", signature.Component("container"));
        Assert.Equal("TryRun`1", signature.Component("metadata"));
        Assert.Equal("1", signature.Component("arity"));
        Assert.Equal("global::System.Threading.Tasks.Task<global::System.Boolean>", signature.Component("type"));
        Assert.Equal("ref global::System.String", signature.Component("parameters"));
        Assert.Equal("global::T", signature.Component("type-arguments"));
    }

    [Fact]
    [Trait("Requirement", "TAX-69")]
    public void Component_OmittedComponentAndUnknownKeyAreBothNull()
    {
        var signature = CanonicalSymbolSignature.Create(
            "namedtype",
            "global::Acme",
            "Order",
            0,
            "global::Acme.Order");

        Assert.Null(signature.Component("parameters"));
        Assert.Null(signature.Component("type-arguments"));
        Assert.Null(signature.Component("no-such-key"));
    }

    [Fact]
    [Trait("Requirement", "TAX-69")]
    public void Component_DoesNotConfuseAKeyWithTheSuffixOfAnother()
    {
        var signature = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme",
            "Run",
            1,
            "global::System.Void",
            typeArguments: ["global::T"]);

        Assert.Equal("global::T", signature.Component("type-arguments"));
        Assert.Equal("global::System.Void", signature.Component("type"));
    }

    [Fact]
    [Trait("Requirement", "APR-16")]
    [Trait("Requirement", "APR-17")]
    public void SplitTopLevel_NamedTupleCommas_AreNotSeparators()
    {
        var slices = CanonicalSymbolSignature.SplitTopLevel(
            "(global::System.String Name, global::System.Int32 Age),global::System.Boolean,(global::System.Int32 X, global::System.Int32 Y)");

        Assert.Equal(
            [
                "(global::System.String Name, global::System.Int32 Age)",
                "global::System.Boolean",
                "(global::System.Int32 X, global::System.Int32 Y)",
            ],
            slices.ToArray());
    }

    [Fact]
    [Trait("Requirement", "APR-18")]
    public void SplitTopLevel_NestedGenericAndTuple_SplitsOnlyAtDepthZero()
    {
        var genericInTuple = CanonicalSymbolSignature.SplitTopLevel(
            "(global::System.Collections.Generic.Dictionary<global::System.String, global::System.Int32> Map, global::System.Boolean Ok),global::System.Int32");
        var tupleInGeneric = CanonicalSymbolSignature.SplitTopLevel(
            "global::System.Collections.Generic.Dictionary<(global::System.String, global::System.Int32), global::System.Int32>,global::System.Boolean");

        Assert.Equal(
            [
                "(global::System.Collections.Generic.Dictionary<global::System.String, global::System.Int32> Map, global::System.Boolean Ok)",
                "global::System.Int32",
            ],
            genericInTuple.ToArray());
        Assert.Equal(
            [
                "global::System.Collections.Generic.Dictionary<(global::System.String, global::System.Int32), global::System.Int32>",
                "global::System.Boolean",
            ],
            tupleInGeneric.ToArray());
    }

    [Fact]
    [Trait("Requirement", "APR-19")]
    public void SplitTopLevel_MultidimensionalArrayRankCommas_AreNotSeparators()
    {
        var slices = CanonicalSymbolSignature.SplitTopLevel(
            "global::System.Int32[,,],global::System.String");

        Assert.Equal(["global::System.Int32[,,]", "global::System.String"], slices.ToArray());
    }

    [Fact]
    [Trait("Requirement", "APR-20")]
    public void SplitTopLevel_MixedShapes_IsDeterministicAcrossTwoCalls()
    {
        const string text =
            "global::System.Collections.Generic.List<(global::System.String Name, global::System.Int32 Age)>,global::System.Int32[,,],(global::System.Boolean Ok, global::System.Byte[] Buffer)";

        var first = CanonicalSymbolSignature.SplitTopLevel(text);
        var second = CanonicalSymbolSignature.SplitTopLevel(text);

        Assert.Equal(first.ToArray(), second.ToArray());
        Assert.Equal(3, first.Length);
        Assert.Equal(
            "global::System.Collections.Generic.List<(global::System.String Name, global::System.Int32 Age)>",
            first[0]);
        Assert.Equal("global::System.Int32[,,]", first[1]);
        Assert.Equal("(global::System.Boolean Ok, global::System.Byte[] Buffer)", first[2]);
    }

    [Fact]
    [Trait("Requirement", "APR-21")]
    public void SplitTopLevel_SimpleAndGenericLists_KeepCurrentSlices()
    {
        var simple = CanonicalSymbolSignature.SplitTopLevel("global::System.String,global::System.Int32");
        var generic = CanonicalSymbolSignature.SplitTopLevel(
            "global::System.Collections.Generic.Dictionary<global::System.String,global::System.Int32>,global::System.Boolean");

        Assert.Equal(["global::System.String", "global::System.Int32"], simple.ToArray());
        Assert.Equal(
            [
                "global::System.Collections.Generic.Dictionary<global::System.String,global::System.Int32>",
                "global::System.Boolean",
            ],
            generic.ToArray());
    }

    [Theory]
    [InlineData("global::System.ValueTuple<global::System.Int32")]
    [InlineData("global::System.ValueTuple(global::System.Int32")]
    [InlineData("global::System.Int32[")]
    [Trait("Requirement", "APR-22")]
    public void SplitTopLevel_UnbalancedDelimiters_ThrowsArgumentException(string text)
    {
        Assert.Throws<ArgumentException>(() => CanonicalSymbolSignature.SplitTopLevel(text));
    }

    [Fact]
    [Trait("Requirement", "APR-23")]
    public void Create_StillJoinsParametersWithCommaAndLeavesIdentityGrammarUnchanged()
    {
        var signature = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payment",
            "Run",
            0,
            "global::System.Void",
            [new("global::System.String"), new("global::System.Int32")]);

        Assert.Equal(
            "sig1;kind=method;container=global%3A%3AAcme.Payment;metadata=Run;arity=0;type=global%3A%3ASystem.Void;parameters=global%3A%3ASystem.String%2Cglobal%3A%3ASystem.Int32;type-arguments=-",
            signature.Value);
    }
}
