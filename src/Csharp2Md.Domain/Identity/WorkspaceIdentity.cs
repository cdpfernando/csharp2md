namespace Csharp2Md.Domain.Identity;

public readonly record struct WorkspaceIdentity
{
    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized workspace identity has no value.");

    public static WorkspaceIdentity Create(string logicalName)
    {
        var id = FactIdGrammar.Create(
            "workspace",
            ("name", FactIdGrammar.RequireCanonicalText(logicalName, nameof(logicalName))));

        return new WorkspaceIdentity(id.Value);
    }

    private WorkspaceIdentity(string value) => _value = value;

    public override string ToString() => Value;
}
