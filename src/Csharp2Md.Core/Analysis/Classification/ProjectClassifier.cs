using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Classification;

internal static class TechnicalComponentKinds
{
    public const string WebApi = "service/web-api";
    public const string Worker = "service/worker";
    public const string Cli = "tool/cli";
    public const string TestSupport = "test-support";
    public const string Library = "library";
}

internal static class ProjectClassifier
{
    internal const string HttpEndpointRelationKind = "http-endpoint";

    private const string MicrosoftTestSdk = "Microsoft.NET.Test.Sdk";
    private const string HostedService = "global::Microsoft.Extensions.Hosting.IHostedService";
    private const string BackgroundService = "global::Microsoft.Extensions.Hosting.BackgroundService";

    public static string? Classify(ProjectFactId projectId, SolutionAnalysisIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        if (index.FindProject(projectId) is null)
        {
            throw new ArgumentException("The project must belong to the indexed solution.", nameof(projectId));
        }

        var confirmedTargets = index.GetTargets(projectId)
            .Where(static target => target.Header.Resolution is FactResolution.Exact && target.Evaluation is not null)
            .ToImmutableArray();
        if (confirmedTargets.IsEmpty)
        {
            return null;
        }

        if (confirmedTargets.Any(static target =>
                target.Evaluation!.PackageReferences.Contains(MicrosoftTestSdk, StringComparer.OrdinalIgnoreCase)))
        {
            return TechnicalComponentKinds.TestSupport;
        }

        if (confirmedTargets.Any(static target => IsExecutable(target.Evaluation!.OutputType)))
        {
            if (index.HasRelation(projectId, RelationPartition.Http, HttpEndpointRelationKind))
            {
                return TechnicalComponentKinds.WebApi;
            }

            if (confirmedTargets.Any(target => HasHostedServiceEvidence(index, target.TargetId)))
            {
                return TechnicalComponentKinds.Worker;
            }

            return TechnicalComponentKinds.Cli;
        }

        return TechnicalComponentKinds.Library;
    }

    private static bool HasHostedServiceEvidence(SolutionAnalysisIndex index, TargetFactId targetId) =>
        !index.GetSymbolsReferencingType(targetId, HostedService).IsEmpty ||
        !index.GetSymbolsReferencingType(targetId, BackgroundService).IsEmpty;

    private static bool IsExecutable(string outputType) =>
        string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase);
}
