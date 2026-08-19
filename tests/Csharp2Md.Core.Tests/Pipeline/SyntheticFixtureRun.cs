using Csharp2Md.Core.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Pipeline;

/// <summary>
/// One full pipeline run against the synthetic fixture, shared by every assertion that inspects its
/// artifacts. A run opens real <c>MSBuildWorkspace</c>s, so it is performed once per test class
/// rather than once per test.
/// </summary>
public sealed class SyntheticFixtureRun : IAsyncLifetime
{
    public const string Orders = "Acme.Orders";
    public const string Payments = "Acme.Payments";
    public const string SharedContracts = "Acme.Shared.Contracts";

    private string _workspace = string.Empty;

    public string OutputRoot { get; private set; } = string.Empty;

    /// <summary>WIKI-01: everything Phase 1 generates, beneath <c>&lt;OutputRoot&gt;/raw</c>.</summary>
    public string RawRoot => TopicLayout.RawRoot(OutputRoot);

    /// <summary>WIKI-02: the mirrored source tree, beneath <c>&lt;OutputRoot&gt;/raw/codebase</c>.</summary>
    public string CodebaseRoot => TopicLayout.CodebaseRoot(OutputRoot);

    public PipelineRunResult Result { get; private set; } = null!;

    public string ServiceOutput(string serviceName) =>
        TopicLayout.ServiceRoot(OutputRoot, new ServiceName(serviceName));

    public async Task InitializeAsync()
    {
        _workspace = Directory.CreateTempSubdirectory("csharp2md-pipeline-").FullName;
        OutputRoot = Path.Combine(_workspace, "output");

        // Manifest order deliberately puts Acme.Shared.Contracts last: a direct-reference edge from
        // Acme.Orders to it can only be produced if Stage 1 completed for every service before any
        // detector ran.
        var manifestPath = FixtureManifest.WriteOverrides(_workspace, Orders, Payments, SharedContracts);

        Result = await new AnalysisPipeline().RunAsync(manifestPath, OutputRoot, CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }

        return Task.CompletedTask;
    }
}
