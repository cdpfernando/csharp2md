using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Rendering;

/// <summary>
/// Renders the "Dependências detectadas" section for one document: communication type, target and
/// resolution classification per detected dependency (P2-10).
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="SemanticEnricher"/> this is an additive pure function over an already-rendered
/// document, and it attaches the section <b>outside</b> the section list rather than as another
/// <see cref="RenderedSection"/>. That is deliberate: a <c>RenderedSection</c> owns a span of the
/// source file, and this section corresponds to no source bytes at all. Adding it to the list would
/// break AD-002's span-coverage invariant, so it travels alongside instead.
/// </para>
/// <para>
/// Messaging signals name the topic or message type as their target (P2-10 as amended). Nothing
/// special is needed for that: a messaging half-edge has no <c>TargetService</c> until Stage 3
/// correlates it, so the raw target — the topic — is what there is to show.
/// </para>
/// </remarks>
public static class DependencySectionRenderer
{
    public const string Heading = "Dependências detectadas";

    /// <summary>Attaches the section, or returns the document untouched when nothing was detected.</summary>
    public static RenderedDocument Apply(RenderedDocument document, IReadOnlyList<DependencySignal> signals)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Render(signals) is { } section
            ? document with { DependencySection = section }
            : document;
    }

    /// <summary>
    /// The section's Markdown, or <c>null</c> when there are no dependencies — P2-10 scopes the
    /// section to files that have one, so a clean file gets no empty heading.
    /// </summary>
    public static string? Render(IReadOnlyList<DependencySignal> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        if (signals.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder("## ").Append(Heading).Append("\n\n");
        builder.Append("| Tipo de comunicação | Alvo | Resolução |\n| --- | --- | --- |\n");

        foreach (var signal in signals)
        {
            builder.Append("| ").Append(Label(signal.Communication))
                .Append(" | ").Append(Target(signal))
                .Append(" | ").Append(Label(signal.Resolution))
                .Append(" |\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// The correlated service when a detector could prove one (T18's direct references), otherwise
    /// the raw target: a logical name, an address, or — for messaging — the topic name.
    /// </summary>
    private static string Target(DependencySignal signal) => signal.TargetService?.Value ?? signal.RawTarget;

    // Same naming policy the JSON writer and the Mermaid writer label with, so one dependency reads
    // identically in the document, in dependencies.json, and in the diagram.
    private static string Label(CommunicationType communication) =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(communication.ToString());

    private static string Label(ResolutionKind resolution) =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(resolution.ToString());
}
