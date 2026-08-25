namespace Csharp2Md.Domain.Identity;

public readonly record struct LogicalKey
{
    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized logical key has no value.");

    public static LogicalKey Create(string key) => new(FactIdGrammar.RequireCanonicalText(key, nameof(key)));

    private LogicalKey(string value) => _value = value;

    public override string ToString() => Value;
}

public readonly record struct ProjectId
{
    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized project identity has no value.");

    public static ProjectId Create(SolutionId solution, string logicalRelativePath, LogicalKey? logicalKey = null)
    {
        var path = FactIdGrammar.ValidateRelativePath(logicalRelativePath, nameof(logicalRelativePath));

        var id = logicalKey is null
            ? FactIdGrammar.Create("project", ("solution", solution.Value), ("path", path))
            : FactIdGrammar.Create("project", ("solution", solution.Value), ("key", logicalKey.Value.Value));

        return new ProjectId(id.Value);
    }

    private ProjectId(string value) => _value = value;

    public override string ToString() => Value;
}
