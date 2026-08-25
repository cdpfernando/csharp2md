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
}
