using Csharp2Md.Analysis.Classification;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ClassifierCapabilityRegistryTests
{
    [Fact]
    [Trait("Requirement", "GCPC-029")]
    public void ConsumedExtensions_PassDeclaringNone_ContributesNoExtensions()
    {
        var registry = new ClassifierCapabilityRegistry([new NonDocumentPass()]);

        Assert.Empty(registry.ConsumedExtensions());
    }

    [Fact]
    [Trait("Requirement", "GCPC-029")]
    public void ConsumedExtensions_PassDeclaringExtensions_AreIncluded()
    {
        var registry = new ClassifierCapabilityRegistry([new ProtoConsumingPass()]);

        Assert.Equal([".proto"], registry.ConsumedExtensions().ToArray());
    }

    [Fact]
    [Trait("Requirement", "GCPC-029")]
    public void ConsumedExtensions_AddingAPass_ChangesTheResult()
    {
        var withoutProtoPass = new ClassifierCapabilityRegistry([new NonDocumentPass()]);
        var withProtoPass = new ClassifierCapabilityRegistry([new NonDocumentPass(), new ProtoConsumingPass()]);

        Assert.DoesNotContain(".proto", withoutProtoPass.ConsumedExtensions());
        Assert.Contains(".proto", withProtoPass.ConsumedExtensions());
    }

    [Fact]
    [Trait("Requirement", "GCPC-029")]
    public void ConsumedExtensions_RemovingAPass_ChangesTheResult()
    {
        var withProtoPass = new ClassifierCapabilityRegistry([new NonDocumentPass(), new ProtoConsumingPass()]);
        var withoutProtoPass = new ClassifierCapabilityRegistry([new NonDocumentPass()]);

        Assert.Contains(".proto", withProtoPass.ConsumedExtensions());
        Assert.DoesNotContain(".proto", withoutProtoPass.ConsumedExtensions());
    }

    [Fact]
    [Trait("Requirement", "GCPC-029")]
    public void ConsumedExtensions_DuplicateAndUnnormalizedDeclarations_AreDeduplicatedAndNormalized()
    {
        var registry = new ClassifierCapabilityRegistry([new ProtoConsumingPass(), new UnnormalizedProtoPass()]);

        Assert.Equal([".proto"], registry.ConsumedExtensions().ToArray());
    }

    [Fact]
    [Trait("Requirement", "GCPC-029")]
    public void ConsumedExtensions_NoPasses_IsEmpty()
    {
        var registry = new ClassifierCapabilityRegistry([]);

        Assert.Empty(registry.ConsumedExtensions());
    }

    private sealed class NonDocumentPass : IClassifierPass
    {
        public string Name => "non-document";

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken) =>
            new(0, 0, 0, 0);
    }

    private sealed class ProtoConsumingPass : IClassifierPass, IDocumentConsumingClassifierPass
    {
        public string Name => "proto";

        public ImmutableArray<string> ConsumedDocumentExtensions => [".proto"];

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken) =>
            new(0, 0, 0, 0);
    }

    private sealed class UnnormalizedProtoPass : IClassifierPass, IDocumentConsumingClassifierPass
    {
        public string Name => "proto-unnormalized";

        public ImmutableArray<string> ConsumedDocumentExtensions => ["proto"];

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken) =>
            new(0, 0, 0, 0);
    }
}
