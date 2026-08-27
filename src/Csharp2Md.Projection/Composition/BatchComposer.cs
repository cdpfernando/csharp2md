using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Composition;

public sealed class BatchComposer : IBatchComposer
{
    public SolutionContribution Contribute(
        PublishedPackageView view,
        SolutionCoordinate coordinate,
        string packageDirectory)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        return new SolutionContribution(
            coordinate.Identity.Value,
            coordinate.SolutionFileName,
            packageDirectory,
            ContributeBoundaryOperations(view),
            ContributeIdentities(view, view.Document.Contracts),
            ContributeNamed(view, view.Document.Components, static dto => dto.Identity, static dto => dto.Name),
            ContributeNamed(view, view.Document.DeploymentUnits, static dto => dto.Identity, static dto => dto.Name),
            ContributeNamed(view, view.Document.ExternalSystems, static dto => dto.Identity, static dto => dto.Name.Value));
    }

    public ImmutableArray<StagedFragment> Compose(BatchView batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return [];
    }

    private static ImmutableArray<ContributedBoundaryOperation> ContributeBoundaryOperations(PublishedPackageView view)
    {
        if (view.Document.BoundaryOperations.IsDefaultOrEmpty)
        {
            return [];
        }

        var contributed = ImmutableArray.CreateBuilder<ContributedBoundaryOperation>();
        foreach (var operation in view.Document.BoundaryOperations)
        {
            if (!view.TryLocate(operation.Identity.Id, out var citation))
            {
                continue;
            }

            contributed.Add(new ContributedBoundaryOperation(
                operation.Identity.Id,
                operation.Identity.FactType,
                operation.Direction,
                operation.Protocol,
                operation.DestinationScope,
                operation.HttpMethod,
                operation.Route?.Value,
                operation.ProtocolOperationKey?.Value,
                citation.ArtifactKey,
                citation.Ordinal));
        }

        return contributed.ToImmutable();
    }

    private static ImmutableArray<ContributedIdentity> ContributeIdentities(
        PublishedPackageView view,
        ImmutableArray<ContractDto> contracts)
    {
        if (contracts.IsDefaultOrEmpty)
        {
            return [];
        }

        var contributed = ImmutableArray.CreateBuilder<ContributedIdentity>();
        foreach (var contract in contracts)
        {
            if (!view.TryLocate(contract.Identity.Id, out var citation))
            {
                continue;
            }

            contributed.Add(new ContributedIdentity(
                contract.Identity.Id,
                contract.Identity.FactType,
                citation.ArtifactKey,
                citation.Ordinal));
        }

        return contributed.ToImmutable();
    }

    private static ImmutableArray<ContributedNamedIdentity> ContributeNamed<T>(
        PublishedPackageView view,
        ImmutableArray<T> facts,
        Func<T, FactReferenceDto> identity,
        Func<T, string> name)
    {
        if (facts.IsDefaultOrEmpty)
        {
            return [];
        }

        var contributed = ImmutableArray.CreateBuilder<ContributedNamedIdentity>();
        foreach (var fact in facts)
        {
            var reference = identity(fact);
            if (!view.TryLocate(reference.Id, out var citation))
            {
                continue;
            }

            contributed.Add(new ContributedNamedIdentity(
                reference.Id,
                reference.FactType,
                name(fact),
                citation.ArtifactKey,
                citation.Ordinal));
        }

        return contributed.ToImmutable();
    }
}
