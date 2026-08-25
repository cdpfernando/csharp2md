using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Literals;

public enum LiteralRole
{
    Route,
    ProtocolName,
    Channel,
    SchemaName,
    TableName,
    FieldName,
    ConfigurationKey,
    ClientName,
}

public readonly record struct StructuralLiteral
{
    public LiteralRole Role { get; }

    public string Value { get; }

    private StructuralLiteral(LiteralRole role, string value)
    {
        Role = role;
        Value = value;
    }

    public static StructuralLiteral Create(LiteralRole role, string value, string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName, nameof(fieldName));
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException($"'{role}' is not a defined value of the '{nameof(LiteralRole)}' axis.", fieldName);
        }

        var canonical = FactIdGrammar.RequireCanonicalText(value, fieldName);
        return new StructuralLiteral(role, canonical);
    }
}
