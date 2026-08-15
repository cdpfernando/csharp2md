namespace Csharp2Md.Core.Output;

public static class OutputPathResolver
{
    public static string? DefaultForInput(string inputRoot)
    {
        ArgumentNullException.ThrowIfNull(inputRoot);

        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(inputRoot));
        var name = Path.GetFileName(normalized);
        var parent = Path.GetDirectoryName(normalized);

        return string.IsNullOrEmpty(name) || string.IsNullOrEmpty(parent)
            ? null
            : Path.Combine(parent, name + "_md");
    }
}
