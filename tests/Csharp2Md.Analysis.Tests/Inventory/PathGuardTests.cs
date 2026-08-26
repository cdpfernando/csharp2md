using Csharp2Md.Analysis.Inventory;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class PathGuardTests
{
    [Fact]
    [Trait("Requirement", "ROSE-02")]
    [Trait("Requirement", "ROSE-07")]
    public void RejectEscapes_SymlinkInsideRootPointingOutside_IsRejectedNamingTheSymlink()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-path-guard-escape-");
        try
        {
            var root = Path.Combine(tree.FullName, "root");
            var outside = Path.Combine(tree.FullName, "outside");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            var target = Path.Combine(outside, "secret.txt");
            File.WriteAllText(target, "classified");
            var symlink = Path.Combine(root, "escape.link");
            File.CreateSymbolicLink(symlink, target);

            var exception = Assert.Throws<InvalidOperationException>(() => PathGuard.RejectEscapes(root, symlink));

            Assert.Contains(symlink, exception.Message, PathComparison());
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-02")]
    [Trait("Requirement", "ROSE-07")]
    public void RejectEscapes_RegularFileInsideRoot_IsAccepted()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-path-guard-inside-");
        try
        {
            var root = Path.Combine(tree.FullName, "root");
            Directory.CreateDirectory(root);
            var inside = Path.Combine(root, "ok.txt");
            File.WriteAllText(inside, "ok");

            PathGuard.RejectEscapes(root, inside);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
