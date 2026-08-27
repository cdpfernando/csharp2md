using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class BatchPublicationPortTests
{
    [Fact]
    [Trait("Requirement", "MSC-03")]
    public void BatchSolutionRecord_CarriesIdentityFileNameStatusAndFailingStage()
    {
        var identity = SolutionId.Create(WorkspaceIdentity.Create("default"), "Acme.Orders.slnx");
        var record = new BatchSolutionRecord(identity, "Acme.Orders.slnx", PublicationStatus.Unpublished, "compile");

        Assert.Equal(identity, record.Identity);
        Assert.Equal("Acme.Orders.slnx", record.SolutionFileName);
        Assert.Equal(PublicationStatus.Committed, new BatchSolutionRecord(identity, "Acme.Orders.slnx", PublicationStatus.Committed, null).Status);
        Assert.Equal(PublicationStatus.Unpublished, record.Status);
        Assert.Equal("compile", record.FailingStage);
        Assert.Null(new BatchSolutionRecord(identity, "Acme.Orders.slnx", PublicationStatus.Committed, null).FailingStage);
    }

    [Fact]
    [Trait("Requirement", "MSC-03")]
    public void BatchSolutionRecord_ExposesNoArtifactKeyOrdinalOrFactType()
    {
        var names = typeof(BatchSolutionRecord)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains(nameof(BatchSolutionRecord.Identity), names);
        Assert.Contains(nameof(BatchSolutionRecord.SolutionFileName), names);
        Assert.Contains(nameof(BatchSolutionRecord.Status), names);
        Assert.Contains(nameof(BatchSolutionRecord.FailingStage), names);
        Assert.DoesNotContain(names, name => name.Contains("Artifact", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Ordinal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("FactType", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("FactId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Requirement", "MSC-03")]
    public void ITransactionalStore_PublishBatch_TakesASolutionRecordArrayAndReturnsVoid()
    {
        var method = typeof(ITransactionalStore).GetMethod(nameof(ITransactionalStore.PublishBatch));

        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(ImmutableArray<BatchSolutionRecord>), parameter.ParameterType);
        Assert.Equal("solutions", parameter.Name);
    }

    [Theory]
    [Trait("Requirement", "MSC-03")]
    [MemberData(nameof(Stores))]
    public void PublishBatch_DefaultOrEmptyRecordArray_ThrowsArgumentException(ITransactionalStore store)
    {
        var defaultException = Assert.Throws<ArgumentException>(() => store.PublishBatch(default));
        Assert.Equal("solutions", defaultException.ParamName);

        var emptyException = Assert.Throws<ArgumentException>(
            () => store.PublishBatch(ImmutableArray<BatchSolutionRecord>.Empty));
        Assert.Equal("solutions", emptyException.ParamName);
    }

    [Theory]
    [Trait("Requirement", "MSC-03")]
    [MemberData(nameof(Stores))]
    public void PublishBatch_NonEmptyRecordArray_DoesNotThrow(ITransactionalStore store)
    {
        var record = new BatchSolutionRecord(
            SolutionId.Create(WorkspaceIdentity.Create("default"), "Acme.Orders.slnx"),
            "Acme.Orders.slnx",
            PublicationStatus.Committed,
            null);

        store.PublishBatch([record]);
    }

    public static TheoryData<ITransactionalStore> Stores() =>
    [
        new InMemoryTransactionalStore(),
        new FilesystemTransactionalStore(Path.Combine(Path.GetTempPath(), "csharp2md-batch-port")),
    ];
}
