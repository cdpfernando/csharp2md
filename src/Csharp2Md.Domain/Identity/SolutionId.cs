namespace Csharp2Md.Domain.Identity;

public readonly record struct SolutionId
{
    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized solution identity has no value.");

    public static SolutionId Create(WorkspaceIdentity workspace, string logicalRelativePath)
    {
        var path = FactIdGrammar.ValidateRelativePath(logicalRelativePath, nameof(logicalRelativePath));

        var id = FactIdGrammar.Create(
            "solution",
            ("workspace", workspace.Value),
            ("path", path));

        return new SolutionId(id.Value);
    }

    private SolutionId(string value) => _value = value;

    public override string ToString() => Value;
}
