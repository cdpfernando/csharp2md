using System.Reflection;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class StageSubstitutionTests
{
    [Fact]
    [Trait("Requirement", "ENG-14")]
    public async Task AnalyzeAsync_RunsAPersistenceSubstituteInPositionSix()
    {
        var executed = new List<string>();
        var probe = new RecordingStage("Persistence", executed);
        var stages = StubStages.CreateDefault().SetItem(5, probe);
        var engine = new AnalysisEngine(new SessionStore(), stages);
        var request = AnalysisRequest.Create(["alpha.sln"]);

        var result = await engine.AnalyzeAsync(request, CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(["Persistence"], executed);
        Assert.Equal(8, outcome.Stages.Length);
        Assert.Equal("Persistence", outcome.Stages[5].Name);
    }

    [Fact]
    [Trait("Requirement", "ENG-14")]
    public void Substitution_DoesNotChangeTheOrchestratorType()
    {
        var store = new SessionStore();
        var defaultEngine = new AnalysisEngine(store);
        var substitutedEngine = new AnalysisEngine(
            store,
            StubStages.CreateDefault().SetItem(5, new RecordingStage("Persistence", [])));

        var defaultOrchestrator = OrchestratorOf(defaultEngine);
        var substitutedOrchestrator = OrchestratorOf(substitutedEngine);

        Assert.IsType<PipelineOrchestrator>(defaultOrchestrator);
        Assert.IsType<PipelineOrchestrator>(substitutedOrchestrator);
        Assert.Equal(defaultOrchestrator.GetType(), substitutedOrchestrator.GetType());
    }

    private static PipelineOrchestrator OrchestratorOf(AnalysisEngine engine)
    {
        var field = typeof(AnalysisEngine).GetField("_orchestrator", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<PipelineOrchestrator>(field.GetValue(engine));
    }
}

internal sealed class SessionStore : ITransactionalStore
{
    public IStoreSession Open(string solutionKey) => new Session(solutionKey);

    private sealed class Session : IStoreSession
    {
        private readonly string _solutionKey;

        public Session(string solutionKey) => _solutionKey = solutionKey;

        public void Stage(StagedFragment fragment)
        {
        }

        public CommittedPublication Commit() => new(_solutionKey, []);

        public void Abort()
        {
        }
    }
}
