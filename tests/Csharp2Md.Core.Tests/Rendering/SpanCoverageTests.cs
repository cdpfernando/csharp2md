namespace Csharp2Md.Core.Tests.Rendering;

/// <summary>
/// AD-002's fidelity invariant, asserted rather than inspected: every byte of the source file
/// lands in exactly one emitted section, and every section's text survives into the Markdown.
/// This is the test that makes structural rendering safe to ship.
/// </summary>
public sealed class SpanCoverageTests
{
    public static TheoryData<string, string> Sources => new()
    {
        { nameof(RenderTestSources.UsingsAndBlockNamespace), RenderTestSources.UsingsAndBlockNamespace },
        { nameof(RenderTestSources.FileScopedNamespaceWithRegions), RenderTestSources.FileScopedNamespaceWithRegions },
        { nameof(RenderTestSources.TopLevelStatements), RenderTestSources.TopLevelStatements },
        { nameof(RenderTestSources.NestedTypesAndEnum), RenderTestSources.NestedTypesAndEnum },
        { nameof(RenderTestSources.RecordWithoutBody), RenderTestSources.RecordWithoutBody },
        { nameof(RenderTestSources.AllMemberKinds), RenderTestSources.AllMemberKinds },
        { nameof(RenderTestSources.BackticksAndNoTrailingNewline), RenderTestSources.BackticksAndNoTrailingNewline },
    };

    [Theory]
    [MemberData(nameof(Sources))]
    public void Sections_PartitionTheSourceFileExactlyOnce(string name, string source)
    {
        var document = RenderTestSources.Render(source);

        Assert.NotEmpty(document.Sections);
        Assert.Equal(0, document.Sections[0].Span.Start);
        Assert.Equal(source.Length, document.Sections[^1].Span.End);

        for (var i = 1; i < document.Sections.Count; i++)
        {
            Assert.Equal(document.Sections[i - 1].Span.End, document.Sections[i].Span.Start);
        }

        Assert.All(document.Sections, section => Assert.True(section.Span.Length > 0, $"{name}: empty section '{section.Title}'"));
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void ConcatenatedSectionText_ReproducesTheSourceByteForByte(string name, string source)
    {
        _ = name; // xUnit uses it to label the case; the assertion below is the whole test.
        var document = RenderTestSources.Render(source);

        Assert.Equal(source, string.Concat(document.Sections.Select(s => s.Text)));
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void EveryDistinctSectionText_AppearsVerbatimInTheMarkdown(string name, string source)
    {
        var document = RenderTestSources.Render(source);
        var markdown = document.ToMarkdown();

        foreach (var section in document.Sections)
        {
            Assert.True(
                markdown.Contains(section.Text, StringComparison.Ordinal),
                $"{name}: section '{section.Title}' text was altered or dropped during Markdown assembly");
        }
    }

    // The code fence must be longer than any backtick run it wraps, otherwise source containing
    // ``` escapes its own block and the Markdown no longer reproduces the source.
    [Fact]
    public void SourceContainingATripleBacktick_IsFencedWithALongerFence()
    {
        var document = RenderTestSources.Render(RenderTestSources.BackticksAndNoTrailingNewline);

        var markdown = document.ToMarkdown();

        Assert.Contains("````csharp", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("\n```csharp", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void TopLevelStatements_EachGetTheirOwnSection()
    {
        var document = RenderTestSources.Render(RenderTestSources.TopLevelStatements, "Program.cs");

        Assert.Equal(3, document.Sections.Count(s => s.Title == "Top-level statement"));
    }

    [Fact]
    public void RegionDirectivesAndInterMemberComments_AreRetainedInSomeSection()
    {
        var document = RenderTestSources.Render(RenderTestSources.FileScopedNamespaceWithRegions, "PaymentService.cs");

        var everything = string.Concat(document.Sections.Select(s => s.Text));

        Assert.Contains("#region Services", everything, StringComparison.Ordinal);
        Assert.Contains("#region Fields", everything, StringComparison.Ordinal);
        Assert.Contains("#endregion", everything, StringComparison.Ordinal);
        Assert.Contains("// A comment between members.", everything, StringComparison.Ordinal);
    }
}
