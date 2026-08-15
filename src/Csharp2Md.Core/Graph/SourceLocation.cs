namespace Csharp2Md.Core.Graph;

/// <summary>Where a dependency signal was observed, used as edge evidence.</summary>
public readonly record struct SourceLocation(string FilePath, int Line)
{
    public override string ToString() => $"{FilePath}:{Line}";
}
