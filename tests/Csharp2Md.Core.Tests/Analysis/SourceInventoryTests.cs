using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Inventory;

namespace Csharp2Md.Core.Tests.Analysis;

public sealed class SourceInventoryTests
{
    [Fact]
    [Trait("Requirement", "PKG-07")]
    public void PathGuard_RegularFileInsideRoot_IsAccepted()
    {
        using var tree = new TempTree();
        var inside = tree.WriteFile("root/ok.cs", "class Ok;");

        PathGuard.RejectEscapes(tree.Root, inside);
    }

    [Fact]
    [Trait("Requirement", "PKG-07")]
    public void PathGuard_SymlinkInsideRootPointingOutside_IsRejected()
    {
        using var tree = new TempTree();
        var target = tree.WriteFile("outside/secret.txt", "classified");
        var symlink = Path.Combine(tree.Root, "escape.link");
        try
        {
            File.CreateSymbolicLink(symlink, target);
        }
        catch (IOException)
        {
            // Creating symlinks may require elevation on some Windows setups.
            return;
        }

        var exception = Assert.Throws<InvalidOperationException>(() => PathGuard.RejectEscapes(tree.Root, symlink));
        Assert.Contains(symlink, exception.Message, PathComparison());
        Assert.Contains("escapes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "PKG-07")]
    public void Collect_ProjectDirectoryOutsideAuthorizedRoot_IsRejected()
    {
        using var tree = new TempTree();
        var outsideProject = Path.Combine(tree.BasePath, "elsewhere");
        Directory.CreateDirectory(outsideProject);

        var exception = Assert.Throws<InvalidOperationException>(
            () => SourceInventory.Collect(tree.Root, outsideProject, new AnalysisPolicy(IncludeTests: false)));
        Assert.Contains("escapes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "PKG-05")]
    [Trait("Requirement", "CRT-08")]
    public void Collect_AcceptsCSharpAndConfiguration_ExcludesUnsupportedAssets()
    {
        using var tree = new TempTree();
        tree.WriteFile("root/src/App/Program.cs", "class Program;");
        tree.WriteFile("root/src/App/appsettings.json", """{"Logging":{}}""");
        tree.WriteFile("root/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        tree.WriteFile("root/src/App/logo.png", "png");

        var result = SourceInventory.Collect(
            tree.Root,
            Path.Combine(tree.Root, "src", "App"),
            new AnalysisPolicy(IncludeTests: false));

        Assert.Contains(result.Accepted, document => document.RelativePath == "src/App/Program.cs"
            && document.Kind == SourceDocumentKind.CSharpSource);
        Assert.Contains(result.Accepted, document => document.RelativePath == "src/App/appsettings.json"
            && document.Kind == SourceDocumentKind.Configuration);
        Assert.Contains(result.Accepted, document => document.RelativePath == "src/App/App.csproj"
            && document.Kind == SourceDocumentKind.ProjectFile);
        Assert.Contains(result.ExcludedRelativePaths, path => path == "src/App/logo.png");
        Assert.All(result.Accepted, document => Assert.DoesNotContain('\\', document.RelativePath));
        Assert.All(result.Accepted, document => Assert.False(Path.IsPathRooted(document.RelativePath)));
    }

    [Fact]
    [Trait("Requirement", "PKG-05")]
    public void Collect_SkipsBinAndObjArtifacts()
    {
        using var tree = new TempTree();
        tree.WriteFile("root/src/App/Program.cs", "class Program;");
        tree.WriteFile("root/src/App/bin/Debug/net10.0/App.dll", "dll");
        tree.WriteFile("root/src/App/obj/Debug/App.cs", "generated");

        var result = SourceInventory.Collect(
            tree.Root,
            Path.Combine(tree.Root, "src", "App"),
            new AnalysisPolicy(IncludeTests: false));

        Assert.Contains(result.Accepted, document => document.RelativePath == "src/App/Program.cs");
        Assert.DoesNotContain(result.Accepted, document => document.RelativePath.Contains("/bin/", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Accepted, document => document.RelativePath.Contains("/obj/", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ExcludedRelativePaths, path => path.Contains("/bin/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "PKG-05")]
    [Trait("Requirement", "PKG-08")]
    public void Collect_TestDocuments_AreExcludedByDefault()
    {
        using var tree = new TempTree();
        tree.WriteFile("root/src/App/Program.cs", "class Program;");
        tree.WriteFile("root/tests/App.Tests/OrderTests.cs", "class OrderTests;");

        var result = SourceInventory.Collect(
            tree.Root,
            tree.Root,
            new AnalysisPolicy(IncludeTests: false));

        Assert.Contains(result.Accepted, document => document.RelativePath == "src/App/Program.cs" && !document.IsTest);
        Assert.Contains(result.ExcludedRelativePaths, path => path == "tests/App.Tests/OrderTests.cs");
        Assert.DoesNotContain(result.Accepted, document => document.RelativePath == "tests/App.Tests/OrderTests.cs");
    }

    [Fact]
    [Trait("Requirement", "PKG-06")]
    [Trait("Requirement", "PKG-08")]
    public void Collect_ExplicitIncludeTests_AdmitsTestDocumentsAndChangesPolicyIdentity()
    {
        using var tree = new TempTree();
        tree.WriteFile("root/src/App/Program.cs", "class Program;");
        tree.WriteFile("root/tests/App.Tests/OrderTests.cs", "class OrderTests;");

        var withoutTests = SourceInventory.Collect(tree.Root, tree.Root, new AnalysisPolicy(IncludeTests: false));
        var withTests = SourceInventory.Collect(tree.Root, tree.Root, new AnalysisPolicy(IncludeTests: true));

        Assert.DoesNotContain(withoutTests.Accepted, document => document.IsTest);
        Assert.Contains(withTests.Accepted, document =>
            document.RelativePath == "tests/App.Tests/OrderTests.cs" && document.IsTest);
        Assert.NotEqual(withoutTests.Policy.PolicyIdentity, withTests.Policy.PolicyIdentity);
        Assert.Equal("analysis-policy/include-tests=false", withoutTests.Policy.PolicyIdentity);
        Assert.Equal("analysis-policy/include-tests=true", withTests.Policy.PolicyIdentity);
    }

    [Fact]
    [Trait("Requirement", "PKG-08")]
    public void AnalysisPolicy_IncludeTestsFlip_ChangesIdentityOnly()
    {
        var off = new AnalysisPolicy(IncludeTests: false);
        var on = new AnalysisPolicy(IncludeTests: true);

        Assert.False(off.IncludeTests);
        Assert.True(on.IncludeTests);
        Assert.NotEqual(off.PolicyIdentity, on.PolicyIdentity);
    }

    [Fact]
    [Trait("Requirement", "PKG-07")]
    public void Collect_SymlinkEscapeDuringEnumeration_IsRejected()
    {
        using var tree = new TempTree();
        tree.WriteFile("root/src/App/Program.cs", "class Program;");
        var target = tree.WriteFile("outside/secret.cs", "class Secret;");
        var symlink = Path.Combine(tree.Root, "src", "App", "leak.cs");
        try
        {
            File.CreateSymbolicLink(symlink, target);
        }
        catch (IOException)
        {
            return;
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => SourceInventory.Collect(
                tree.Root,
                Path.Combine(tree.Root, "src", "App"),
                new AnalysisPolicy(IncludeTests: false)));
        Assert.Contains("escapes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "CRT-08")]
    public void Classify_RecognizesSourceConfigAndUnsupportedKinds()
    {
        Assert.Equal(SourceDocumentKind.CSharpSource, SourceInventory.Classify("src/App/Program.cs"));
        Assert.Equal(SourceDocumentKind.Configuration, SourceInventory.Classify("src/App/appsettings.Development.json"));
        Assert.Equal(SourceDocumentKind.ProjectFile, SourceInventory.Classify("src/App/App.csproj"));
        Assert.Equal(SourceDocumentKind.Unsupported, SourceInventory.Classify("src/App/readme.md"));
    }

    [Theory]
    [Trait("Requirement", "PKG-08")]
    [InlineData("tests/App.Tests/Foo.cs", true)]
    [InlineData("src/App.UnitTests/Foo.cs", true)]
    [InlineData("src/App/Program.cs", false)]
    [InlineData("src/SistemaA.Testes/Foo.cs", true)]
    [InlineData("src/Testemunho/Foo.cs", false)]
    [InlineData("src/Manifesto/Foo.cs", false)]
    public void LooksLikeTestDocument_UsesPathSegments(string relativePath, bool expected)
    {
        Assert.Equal(expected, SourceInventory.LooksLikeTestDocument(relativePath));
    }

    [Fact]
    [Trait("Requirement", "PKG-05")]
    public void IsTestProject_RecognizesThePortugueseTestesConvention()
    {
        var solution = CanonicalIdentity.CreateSolution("sistema", "Sistema.slnx");
        var testProject = CanonicalIdentity.CreateProject(solution, "src/SistemaA.Testes/SistemaA.Testes.csproj");
        var productionProject = CanonicalIdentity.CreateProject(solution, "src/SistemaA.Api/SistemaA.Api.csproj");

        Assert.True(SourceInventory.IsTestProject(testProject));
        Assert.False(SourceInventory.IsTestProject(productionProject));
    }

    [Fact]
    [Trait("Requirement", "PKG-06")]
    public void Collect_AcceptedDocuments_CarryStableContentDigests()
    {
        using var tree = new TempTree();
        tree.WriteFile("root/src/App/Program.cs", "class Program;");

        var first = SourceInventory.Collect(
            tree.Root,
            Path.Combine(tree.Root, "src", "App"),
            new AnalysisPolicy(IncludeTests: false));
        var second = SourceInventory.Collect(
            tree.Root,
            Path.Combine(tree.Root, "src", "App"),
            new AnalysisPolicy(IncludeTests: false));

        var firstProgram = Assert.Single(first.Accepted, document => document.RelativePath == "src/App/Program.cs");
        var secondProgram = Assert.Single(second.Accepted, document => document.RelativePath == "src/App/Program.cs");
        Assert.Equal(firstProgram.ContentDigest, secondProgram.ContentDigest);
        Assert.Equal(firstProgram.ByteLength, secondProgram.ByteLength);
        Assert.NotEqual(string.Empty, firstProgram.ContentDigest);
    }

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed class TempTree : IDisposable
    {
        private readonly string _basePath;

        public TempTree()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "csharp2md-source-inventory-" + Guid.NewGuid().ToString("N"));
            Root = Path.Combine(_basePath, "root");
            Directory.CreateDirectory(Root);
        }

        public string BasePath => _basePath;

        public string Root { get; }

        public string WriteFile(string relativePath, string contents)
        {
            var fullPath = Path.Combine(_basePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
            return fullPath;
        }

        public void Dispose() => TempPath.TryDelete(_basePath);
    }
}
