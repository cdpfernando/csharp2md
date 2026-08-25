using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests;

public sealed class AnalysisEngineTests
{
    [Fact]
    [Trait("Requirement", "ENG-03")]
    public void AnalysisEngine_IsConstructibleWithAFakeStore()
    {
        IAnalysisEngine engine = new AnalysisEngine(new FakeTransactionalStore());

        Assert.IsType<AnalysisEngine>(engine);
        Assert.IsAssignableFrom<IAnalysisEngine>(engine);
    }

    private sealed class FakeTransactionalStore : ITransactionalStore
    {
        public IStoreSession Open(string solutionKey) =>
            throw new NotImplementedException();
    }
}
