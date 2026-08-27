using System.Reflection;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests;

public sealed class BatchComposerPortTests
{
    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void IBatchComposer_Compose_AcceptsOnlyBatchView()
    {
        var method = typeof(IBatchComposer).GetMethod(nameof(IBatchComposer.Compose));

        Assert.NotNull(method);
        Assert.Equal(typeof(ImmutableArray<StagedFragment>), method.ReturnType);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(BatchView), parameter.ParameterType);
        Assert.Equal("batch", parameter.Name);
        Assert.DoesNotContain(
            method.GetParameters(),
            candidate => candidate.ParameterType == typeof(PublishedPackageView)
                || candidate.ParameterType == typeof(WireDocument)
                || candidate.ParameterType == typeof(FactualSnapshot));
    }

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void IBatchComposer_Contribute_ReadsAPublishedViewAndReturnsAContribution()
    {
        var method = typeof(IBatchComposer).GetMethod(nameof(IBatchComposer.Contribute));

        Assert.NotNull(method);
        Assert.Equal(typeof(SolutionContribution), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(PublishedPackageView), parameters[0].ParameterType);
        Assert.Equal("view", parameters[0].Name);
        Assert.Equal(typeof(SolutionCoordinate), parameters[1].ParameterType);
        Assert.Equal("coordinate", parameters[1].Name);
        Assert.Equal(typeof(string), parameters[2].ParameterType);
        Assert.Equal("packageDirectory", parameters[2].Name);
    }

    [Fact]
    [Trait("Requirement", "MSC-10")]
    [Trait("Requirement", "MSC-11")]
    [Trait("Requirement", "MSC-25")]
    public void BatchView_AnyUnpublishedRecord_IsIncompleteWithSolutionUnpublishedReason()
    {
        var view = new BatchView([Committed("orders"), Unpublished("payments")], []);

        Assert.False(view.Complete);
        Assert.Equal("solution-unpublished", view.IncompleteScopeReason);
    }

    [Fact]
    [Trait("Requirement", "MSC-11")]
    [Trait("Requirement", "MSC-25")]
    public void BatchView_EveryRecordCommitted_IsCompleteWithNoIncompleteScopeReason()
    {
        var view = new BatchView([Committed("orders"), Committed("payments")], []);

        Assert.True(view.Complete);
        Assert.Null(view.IncompleteScopeReason);
    }

    [Fact]
    [Trait("Requirement", "MSC-10")]
    [Trait("Requirement", "MSC-25")]
    public void BatchView_CompleteAndIncompleteScopeReason_AreNotCallerSuppliedConstructorParameters()
    {
        var ctor = typeof(BatchView).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var names = ctor.GetParameters().Select(parameter => parameter.Name ?? string.Empty).ToArray();

        Assert.Equal(new[] { "Solutions", "Contributions" }, names);
        Assert.DoesNotContain("Complete", names, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("IncompleteScopeReason", names, StringComparer.OrdinalIgnoreCase);
    }

    private static BatchSolutionRecord Committed(string fileName) =>
        new(SolutionId.Create(WorkspaceIdentity.Create("default"), fileName), fileName, PublicationStatus.Committed, null);

    private static BatchSolutionRecord Unpublished(string fileName) =>
        new(SolutionId.Create(WorkspaceIdentity.Create("default"), fileName), fileName, PublicationStatus.Unpublished, "compile");
}
