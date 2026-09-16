using System.Text.Json;

namespace Csharp2Md.Cli.Tests;

public sealed class SyntheticSolutionFixtureTests
{
    private static readonly string Root = Path.Combine(CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution");

    [Fact]
    public void Fixture_HasOnlySourceInputs_NoBuildOutputs() =>
        Assert.Empty(Directory.EnumerateDirectories(Root, "bin", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateDirectories(Root, "obj", SearchOption.AllDirectories)));

    [Fact]
    public void Fixture_HasExpectedServiceRoots()
    {
        Assert.True(Directory.Exists(Path.Combine(Root, "Acme.Orders")));
        Assert.True(Directory.Exists(Path.Combine(Root, "Acme.Payments")));
        Assert.True(Directory.Exists(Path.Combine(Root, "Acme.Shipping")));
    }

    [Fact]
    public void Oracle_NamesExpectedRoots() =>
        Assert.Equal(["Acme.Orders", "Acme.Payments", "Acme.Shipping"], Values("expectedRoots"));

    [Fact]
    public void Oracle_NamesExpectedBoundaryEdges() =>
        Assert.Equal(
            [
                "Acme.Orders->PaymentService:http",
                "Acme.Orders->ShippingService:http",
                "Acme.Orders->Payments:grpc",
                "Acme.Orders->OrderPlaced:publish",
                "Acme.Shipping->OrderPlaced:subscribe",
            ],
            Values("expectedEdges"));

    [Fact]
    public void Oracle_NamesVariantOccurrences() =>
        Assert.Equal(
            ["Acme.Shared.Contracts:net8.0", "Acme.Shared.Contracts:net10.0", "Acme.Shipping:net10.0"],
            Values("expectedOccurrences"));

    [Fact]
    public void Oracle_NamesCausalCycle() =>
        Assert.Equal("CycleProbe.Start->CycleProbe.Continue->CycleProbe.Start", Value("expectedCycle"));

    [Fact]
    public void Oracle_NamesControlledGap() =>
        Assert.Equal("ShippingService is unresolved", Value("expectedGap"));

    [Fact]
    public void Oracle_NamesExcludedTestAndSafetyValues() =>
        Assert.Equal(
            ["appsettings-fixture-secret", "inline-fixture-secret", "C:\\fixture\\synthetic-output", "Acme.Shipping.Tests"],
            Values("excludedValues"));

    [Fact]
    public void SharedContracts_IsMultiTargeted()
    {
        var project = File.ReadAllText(Path.Combine(Root, "Acme.Shared.Contracts", "Acme.Shared.Contracts.csproj"));

        Assert.Contains("<TargetFrameworks>net8.0;net10.0</TargetFrameworks>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippingSolution_ContainsExcludedTestProject()
    {
        var solution = File.ReadAllText(Path.Combine(Root, "Acme.Shipping", "Acme.Shipping.slnx"));
        var testProject = Path.Combine(Root, "Acme.Shipping.Tests", "Acme.Shipping.Tests.csproj");

        Assert.Contains("Acme.Shipping.Tests/Acme.Shipping.Tests.csproj", solution, StringComparison.Ordinal);
        Assert.True(File.Exists(testProject));
        Assert.True(File.Exists(Path.Combine(Root, "Acme.Shipping.Tests", "ShippingScenarioTests.cs")));
    }

    [Fact]
    public void Orders_HasRepeatedBoundaryCalls()
    {
        var source = File.ReadAllText(Path.Combine(Root, "Acme.Orders", "OrderService.cs"));

        Assert.True(Count(source, "CreateClient(") >= 3);
    }

    [Fact]
    public void Orders_HasRuntimeIntegrationAndComponentDependency()
    {
        var source = File.ReadAllText(Path.Combine(Root, "Acme.Orders", "OrderService.cs"));

        Assert.Contains("IEventBus", source, StringComparison.Ordinal);
        Assert.Contains("PaymentClient", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_ContainsCausalCycleProbe()
    {
        var source = File.ReadAllText(Path.Combine(Root, "Acme.Shared.Contracts", "CycleProbe.cs"));

        Assert.Contains("Start() => Continue()", source, StringComparison.Ordinal);
        Assert.Contains("Continue() => Start()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_ContainsSafetyInputsAndOracleNamesThemForExclusion()
    {
        var config = File.ReadAllText(Path.Combine(Root, "Acme.Orders", "appsettings.json"));
        var sql = File.ReadAllText(Path.Combine(Root, "Acme.Orders", "Data", "OrderSqlQueries.cs"));

        Assert.Contains("appsettings-fixture-secret", config, StringComparison.Ordinal);
        Assert.Equal(
            "C:\\fixture\\synthetic-output",
            JsonDocument.Parse(config).RootElement.GetProperty("FixturePaths").GetProperty("LocalOutput").GetString());
        Assert.Contains("inline-fixture-secret", sql, StringComparison.Ordinal);
        Assert.Contains("// Hand-written SQL", sql, StringComparison.Ordinal);
        Assert.Equal(
            ["appsettings-fixture-secret", "inline-fixture-secret", "C:\\fixture\\synthetic-output", "Acme.Shipping.Tests"],
            Values("excludedValues"));
    }

    private static string Value(string name) => Oracle().GetProperty(name).GetString()!;
    private static string[] Values(string name) => Oracle().GetProperty(name).EnumerateArray().Select(static value => value.GetString()!).ToArray();
    private static JsonElement Oracle() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "knowledge-oracle.json"))).RootElement.Clone();
    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;
}
