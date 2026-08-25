namespace Csharp2Md.Analysis.Tests.Isolation;

public sealed class PortLedgerTests
{
    private static readonly string LedgerRelativePath = Path.Combine(
        "docs",
        "architecture",
        "legacy-port-ledger.md");

    public static TheoryData<string> DesignReuseTablePaths =>
    [
        "src/Csharp2Md.Core/Analysis",
        "src/Csharp2Md.Core/Facts",
        "schemas",
        "src/Csharp2Md.Core/Analysis/Semantics",
        "src/Csharp2Md.Core/Projection",
        "benchmarks/Csharp2Md.RetrievalIndex.Benchmarks",
    ];

    [Fact]
    [Trait("Requirement", "ENG-52")]
    public void Ledger_ExistsAtTheArchitecturePath()
    {
        Assert.True(
            File.Exists(LedgerPath()),
            $"Port ledger was not found at '{LedgerPath()}'.");
    }

    [Theory]
    [Trait("Requirement", "ENG-52")]
    [MemberData(nameof(DesignReuseTablePaths))]
    public void Ledger_RecordsLastCommitShaForDesignReuseTablePath(string formerPath)
    {
        var row = Rows().FirstOrDefault(candidate => PathsEqual(candidate.FormerPath, formerPath));

        Assert.True(
            row is not null,
            $"Port ledger has no row whose former path is '{formerPath}'. Recorded paths: {FormatPaths()}.");
        Assert.True(
            IsGitSha(row!.LastCommit),
            $"Port ledger row for '{formerPath}' must record a 40-character git SHA, but Last commit is '{row.LastCommit}'.");
        Assert.False(
            string.IsNullOrWhiteSpace(row.Responsibility),
            $"Port ledger row for '{formerPath}' must record a one-line responsibility.");
    }

    [Fact]
    [Trait("Requirement", "ENG-52")]
    public void Ledger_RecordsLastCommitShaOnEveryRow()
    {
        var rows = Rows();
        Assert.True(rows.Count > 0, "Port ledger contains no data rows.");

        var missingSha = rows
            .Where(row => !IsGitSha(row.LastCommit))
            .Select(row => row.Area)
            .ToArray();

        Assert.True(
            missingSha.Length == 0,
            $"Port ledger row(s) missing a 40-character git SHA: {string.Join(", ", missingSha)}.");
    }

    [Fact]
    [Trait("Requirement", "ENG-53")]
    public void Ledger_NamesRoslynSanitationProbesForWorkstream4()
    {
        var row = NamedRow("Roslyn sanitation probes");

        Assert.Equal(
            "tests/Csharp2Md.Core.Tests/Analysis/Viability/RoslynSanitationProbeTests.cs",
            NormalizePath(row.FormerPath));
        Assert.Equal("workstream 4", row.ReEstablishIn);
    }

    [Fact]
    [Trait("Requirement", "ENG-53")]
    public void Ledger_NamesCliSecurityBoundaryTestsForWorkstream8()
    {
        var row = NamedRow("CLI security-boundary tests");

        Assert.Equal(
            "tests/Csharp2Md.Core.Tests/Cli/V3SecurityBoundaryTests.cs",
            NormalizePath(row.FormerPath));
        Assert.Equal("workstream 8", row.ReEstablishIn);
    }

    private static LedgerRow NamedRow(string area)
    {
        var row = Rows().FirstOrDefault(candidate => candidate.Area == area);
        Assert.True(row is not null, $"Port ledger has no row named '{area}'.");
        return row!;
    }

    private static IReadOnlyList<LedgerRow> Rows()
    {
        var lines = File.ReadAllLines(LedgerPath());
        var headerIndex = Array.FindIndex(
            lines,
            line => line.StartsWith("|", StringComparison.Ordinal)
                && line.Contains("Area", StringComparison.Ordinal)
                && line.Contains("Former path", StringComparison.Ordinal));

        Assert.True(headerIndex >= 0, "Port ledger has no markdown table with Area and Former path columns.");

        var rows = new List<LedgerRow>();
        for (var i = headerIndex + 2; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.StartsWith('|'))
            {
                break;
            }

            var cells = line.Split('|', StringSplitOptions.TrimEntries);
            // Split on a leading/trailing pipe yields empty first and last cells.
            Assert.True(
                cells.Length >= 7,
                $"Port ledger table row is missing columns: '{line}'.");

            rows.Add(new LedgerRow(
                Unfence(cells[1]),
                Unfence(cells[2]),
                Unfence(cells[3]),
                Unfence(cells[4]),
                Unfence(cells[5])));
        }

        return rows;
    }

    private static string LedgerPath() =>
        Path.Combine(AnalysisTestPaths.RepoRoot, LedgerRelativePath);

    private static string FormatPaths() =>
        string.Join(", ", Rows().Select(row => row.FormerPath));

    private static bool PathsEqual(string recorded, string expected) =>
        string.Equals(NormalizePath(recorded), NormalizePath(expected), StringComparison.Ordinal);

    private static string Unfence(string cell)
    {
        var trimmed = cell.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '`' && trimmed[^1] == '`'
            ? trimmed[1..^1]
            : trimmed;
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim().TrimEnd('/');

    private static bool IsGitSha(string value) =>
        value.Length == 40 && value.All(char.IsAsciiHexDigit);

    private sealed record LedgerRow(
        string Area,
        string FormerPath,
        string Responsibility,
        string LastCommit,
        string ReEstablishIn);
}
