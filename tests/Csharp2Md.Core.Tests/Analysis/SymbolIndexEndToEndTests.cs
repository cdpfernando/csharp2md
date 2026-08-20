using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Tests.Pipeline;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// spec.md's P1 Independent Test, run literally: the index is built by a real
/// <see cref="AnalysisEngine.AnalyzeAsync"/> pass over <c>fixtures/SyntheticSolution</c> - not from
/// hand-built unit-test facts - in both the default syntax-only mode and trusted-solution mode.
/// This is the feature's third Goal: proof the live pipeline actually invokes the index.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SymbolIndexEndToEndTests(SymbolIndexEndToEndFixture fixture)
    : IClassFixture<SymbolIndexEndToEndFixture>
{
    [Fact]
    public void P1_SyntaxOnlyRun_FindByNameReturnsTheRealPaymentsServiceTypeWithSyntacticResolution()
    {
        var type = Assert.Single(fixture.SyntaxOnly.FindByName("PaymentsService"), IsClass);

        Assert.Equal("PaymentsService", type.Name);
        Assert.Equal("Acme.Payments", type.Namespace);
        Assert.Equal("global::Acme.Payments.PaymentsService", type.FullyQualifiedName);
        Assert.Equal(FactResolution.Syntactic, type.Header.Resolution);
    }

    [Fact]
    public void P1_SyntaxOnlyRun_FindMembersReturnsTheRealAuthorizePaymentMethodWithSyntacticResolution()
    {
        var method = Assert.Single(fixture.SyntaxOnly.FindMembers("PaymentsService", "AuthorizePayment"));

        Assert.Equal("AuthorizePayment", method.Name);
        Assert.Equal("global::Acme.Payments.PaymentsService", method.ContainingType);
        Assert.Equal(2, method.ParameterTypes.Length);
        Assert.Equal(FactResolution.Syntactic, method.Header.Resolution);
    }

    /// <summary>
    /// spec.md's P1 Independent Test asks for <c>Resolution = Exact</c> in trusted mode. It names
    /// <c>PaymentsService</c>/<c>AuthorizePayment</c>, which cannot bind on this fixture: the type's
    /// base <c>Payments.PaymentsBase</c> is gRPC-generated and the method's parameters are
    /// <c>Grpc.Core</c> types, none of which the fixture compilation references. This asserts the
    /// same criterion on a type and member that do bind, so the Exact path is genuinely covered;
    /// the test below pins what actually happens to the two symbols the spec named.
    /// </summary>
    [Fact]
    public void P1_TrustedSolutionRun_SurfacesExactResolutionThroughTheLiveIndex()
    {
        var types = fixture.Trusted.FindByName("SwaggerOperationDefaultsFilter");
        var members = fixture.Trusted.FindMembers("SwaggerOperationDefaultsFilter", "Apply");

        var type = Assert.Single(types, symbol => symbol.Header.Resolution == FactResolution.Exact);
        var method = Assert.Single(members, symbol => symbol.Header.Resolution == FactResolution.Exact);

        Assert.Equal("Acme.Orders.Api", type.Namespace);
        Assert.False(type.ContainsErrorSymbol);
        Assert.Equal("Apply", method.Name);
        Assert.Equal("global::Acme.Orders.Api.SwaggerOperationDefaultsFilter", method.ContainingType);
        Assert.False(method.ContainsErrorSymbol);
    }

    /// <summary>
    /// spec.md P1 criterion 9 and the <c>ContainsErrorSymbol</c> edge case: a declaration the
    /// SemanticModel could not bind is still indexed, at its own resolution level, never upgraded
    /// to Exact by the index.
    /// </summary>
    [Fact]
    public void P1_TrustedSolutionRun_PaymentsServiceFailsToBindAndIsIndexedUnresolvedNeverExact()
    {
        var type = Assert.Single(fixture.Trusted.FindByName("PaymentsService"), IsClass);
        var method = Assert.Single(fixture.Trusted.FindMembers("PaymentsService", "AuthorizePayment"));

        Assert.Equal(FactResolution.Unresolved, type.Header.Resolution);
        Assert.Equal(FactResolution.Unresolved, method.Header.Resolution);
        Assert.NotEqual(FactResolution.Exact, type.Header.Resolution);
        Assert.NotEqual(FactResolution.Exact, method.Header.Resolution);
        Assert.True(type.ContainsErrorSymbol);
        Assert.True(method.ContainsErrorSymbol);
        Assert.Equal("Acme.Payments", type.Namespace);
        Assert.Equal("global::Acme.Payments.PaymentsService", method.ContainingType);
    }

    [Fact]
    public void P1_BothModes_BuildTheIndexFromTheLiveRunRatherThanLeavingItEmpty()
    {
        Assert.NotEmpty(fixture.SyntaxOnly.Symbols);
        Assert.NotEmpty(fixture.Trusted.Symbols);
        Assert.Equal(fixture.SyntaxOnly.Symbols.Length, fixture.SyntaxOnly.Metrics.TotalSymbols);
        Assert.True(fixture.SyntaxOnly.Metrics.ByKind[IndexedSymbolKind.Class] > 0);

        // spec.md SYNIDX-23: syntax-only mode has no trust precondition - it indexes the whole
        // solution, and every entry it produces is Syntactic (never a fabricated Exact).
        Assert.Equal(
            [FactResolution.Syntactic],
            fixture.SyntaxOnly.Metrics.ByResolution.Keys.Order().ToArray());
        Assert.Contains(FactResolution.Exact, fixture.Trusted.Metrics.ByResolution.Keys);
    }

    private static bool IsClass(SymbolFact symbol) =>
        string.Equals(symbol.SymbolKind, "class", StringComparison.Ordinal);
}

public sealed class SymbolIndexEndToEndFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    private readonly List<string> _outputs = [];

    internal SymbolIndex SyntaxOnly { get; private set; } = null!;

    internal SymbolIndex Trusted { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        SyntaxOnly = await RunAsync(new AnalysisOptions());
        Trusted = await RunAsync(new AnalysisOptions
        {
            Mode = AnalysisMode.Semantic,
            Trust = TrustMode.TrustedSolution,
        });
    }

    public Task DisposeAsync()
    {
        foreach (var output in _outputs.Where(Directory.Exists))
        {
            Directory.Delete(output, recursive: true);
        }

        return Task.CompletedTask;
    }

    private async Task<SymbolIndex> RunAsync(AnalysisOptions options)
    {
        var output = Path.Combine(Path.GetTempPath(), $"csharp2md-symidx-e2e-{Guid.NewGuid():N}");
        _outputs.Add(output);
        var manifestDirectory = Directory.CreateTempSubdirectory("csharp2md-symidx-e2e-manifest-").FullName;
        var manifest = FixtureManifest.WriteOverrides(manifestDirectory, Projects);

        var request = Assert.IsType<AnalysisRequest>(AnalysisRequest.Create(
            manifest, output, options: options,
            topic: "acme-symbol-index", domain: "system-design").Request);

        SymbolIndex? captured = null;
        var engine = new AnalysisEngine(
            new InertInventory(), FactValidator.Validate, null, index => captured = index);
        var result = await engine.AnalyzeAsync(request);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"fixture analysis failed with exit {result.ExitCode}: {string.Join(" | ", result.Diagnostics)}");
        }

        return captured ?? throw new InvalidOperationException("AnalyzeAsync never built a SymbolIndex.");
    }
}
