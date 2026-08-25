using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Facts;

public sealed record ConfigurationBinding : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Configuration;

    public FactReference BoundFact { get; }

    public StructuralLiteral ConfigurationKey { get; }

    private ConfigurationBinding(FactReference reference, FactReference boundFact, StructuralLiteral configurationKey)
    {
        Reference = reference;
        BoundFact = boundFact;
        ConfigurationKey = configurationKey;
    }

    public static ConfigurationBinding Create(FactReference boundFact, StructuralLiteral configurationKey)
    {
        FactGuards.RequireInitialized(boundFact, nameof(boundFact));
        FactGuards.RequireInitialized(configurationKey, nameof(configurationKey));
        if (configurationKey.Role != LiteralRole.ConfigurationKey)
        {
            throw new ArgumentException(
                $"A configuration binding's key must be a structural literal with role '{nameof(LiteralRole.ConfigurationKey)}', but was '{configurationKey.Role}'.",
                nameof(configurationKey));
        }

        var id = FactIdGrammar.Create("configuration-binding", ("project", boundFact.Id.Value), ("key", configurationKey.Value));
        var reference = new FactReference(id, nameof(ConfigurationBinding));
        return new ConfigurationBinding(reference, boundFact, configurationKey);
    }
}
