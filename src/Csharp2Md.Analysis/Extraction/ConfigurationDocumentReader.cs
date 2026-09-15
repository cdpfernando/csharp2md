using System.Globalization;
using System.Text.Json;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Extraction;

internal static class ConfigurationDocumentReader
{
    /// <summary>
    /// The configuration-parsing policy stated explicitly (GCPC-105), matching what
    /// <c>Microsoft.Extensions.Configuration.Json</c> accepts: line comments (<c>//</c>), block
    /// comments (<c>/* */</c>), a trailing comma before a closing <c>}</c> or <c>]</c>, and a
    /// leading UTF-8 byte-order mark. A duplicate key at the same level (compared
    /// case-insensitively, matching configuration key comparison) is rejected, also matching the
    /// provider. Carried into provenance by a later phase.
    /// </summary>
    public const string ParsingPolicy =
        "Accepts line comments, block comments, a trailing comma and a UTF-8 BOM; "
            + "rejects a duplicate key (case-insensitive), matching Microsoft.Extensions.Configuration.Json.";

    private static readonly BindingDiagnostic Configured = new("configured", "configured");

    private static readonly JsonDocumentOptions TolerantOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static int Emit(PipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var count = 0;
        foreach (var document in context.ConfigurationDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count += EmitDocument(context, document);
        }

        return count;
    }

    private static int EmitDocument(PipelineContext context, Document document)
    {
        var absolute = Path.GetFullPath(
            Path.Combine(
                context.AuthorizedRoot,
                document.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        PathGuard.RejectEscapes(context.AuthorizedRoot, absolute);

        var hash = ObservationMaterializer.HashFileBytes(absolute);
        var locator = ObservationMaterializer.CreateWholeDocumentLocator(document, absolute);

        JsonDocument parsed;
        try
        {
            var bytes = File.ReadAllBytes(absolute);
            parsed = JsonDocument.Parse(StripUtf8Bom(bytes), TolerantOptions);
        }
        catch (JsonException)
        {
            context.Accumulator.AddDiagnostic(new DiagnosticRecord(
                "malformed-configuration-document",
                $"The configuration document '{document.RelativePath}' could not be parsed as JSON.",
                document.RelativePath));
            return 0;
        }

        using (parsed)
        {
            var leaves = new List<Leaf>();
            Walk(parsed.RootElement, path: "", leaves);

            if (HasDuplicateKey(leaves))
            {
                context.Accumulator.AddDiagnostic(new DiagnosticRecord(
                    "malformed-configuration-document",
                    $"The configuration document '{document.RelativePath}' contains a duplicate key, "
                        + "which the .NET configuration provider rejects.",
                    document.RelativePath));
                return 0;
            }

            leaves.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.KeyPath, right.KeyPath));

            var ordinal = 1;
            foreach (var leaf in leaves)
            {
                var draft = Materialize(document.Reference, leaf, locator, hash);
                RedactLeafValue(draft, leaf.Value, context);
                context.Accumulator.AddObservation(
                    Observation.Create(
                        draft.Owner,
                        draft.Kind,
                        draft.Payload,
                        ordinal,
                        draft.Locator,
                        draft.ExtractionMethod,
                        draft.Diagnostic,
                        draft.DocumentHash,
                        ObservationMaterializer.Version));
                ordinal++;
            }

            return leaves.Count;
        }
    }

    private static bool HasDuplicateKey(List<Leaf> leaves)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var leaf in leaves)
        {
            if (!seen.Add(leaf.KeyPath))
            {
                return true;
            }
        }

        return false;
    }

    private static ReadOnlyMemory<byte> StripUtf8Bom(byte[] bytes)
    {
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return hasBom ? bytes.AsMemory(3) : bytes;
    }

    private static ObservationDraft Materialize(
        FactReference owner,
        Leaf leaf,
        EvidenceLocator locator,
        DocumentHash hash)
    {
        var secret = leaf.Value is not null && SecretRedactor.TryRedact(leaf.Value, out _);
        var entries = new List<PayloadEntry>
        {
            new("key", StructuralLiteral.Create(LiteralRole.ConfigurationKey, leaf.KeyPath, "key")),
            new("resolution", StructuralLiteral.Create(LiteralRole.ConfigurationKey, Resolve(leaf.Value), "resolution")),
        };
        if (!secret && TryAbsoluteUri(leaf.Value, out var address))
        {
            entries.Add(new PayloadEntry(
                "address",
                StructuralLiteral.Create(LiteralRole.ConfigurationKey, address, "address")));
        }

        return new ObservationDraft(
            owner,
            ObservationKind.Configuration,
            NormalizedPayload.Create(entries),
            locator,
            EvidenceMethod.Configured,
            Configured,
            hash);
    }

    private static void RedactLeafValue(ObservationDraft draft, string? value, PipelineContext context)
    {
        if (value is null || !TryLiteral(value, out var literal))
        {
            return;
        }

        ObservationMaterializer.Redact(
            draft with { Payload = NormalizedPayload.Create([new PayloadEntry("value", literal)]) },
            context.Accumulator);
    }

    private static bool TryLiteral(string value, out StructuralLiteral literal)
    {
        try
        {
            literal = StructuralLiteral.Create(LiteralRole.ConfigurationKey, value, "value");
            return true;
        }
        catch (ArgumentException)
        {
            literal = default;
            return false;
        }
    }

    private static void Walk(JsonElement element, string path, List<Leaf> leaves)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, Join(path, property.Name), leaves);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, Join(path, index.ToString(CultureInfo.InvariantCulture)), leaves);
                    index++;
                }

                break;
            default:
                if (!string.IsNullOrEmpty(path))
                {
                    leaves.Add(new Leaf(path, ScalarValue(element)));
                }

                break;
        }
    }

    private static string Join(string path, string segment) =>
        string.IsNullOrEmpty(path) ? segment : path + ":" + segment;

    private static string? ScalarValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText(),
        };

    private static string Resolve(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "unknown";
        }

        if (IsDynamic(value))
        {
            return "dynamic";
        }

        return "literal";
    }

    private static bool IsDynamic(string value) =>
        IsWrapped(value, "${", "}")
        || IsWrapped(value, "%", "%")
        || (value.Length > 1 && value[0] == '$' && value[1] != '{');

    private static bool IsWrapped(string value, string prefix, string suffix) =>
        value.StartsWith(prefix, StringComparison.Ordinal)
        && value.EndsWith(suffix, StringComparison.Ordinal)
        && value.Length > prefix.Length + suffix.Length;

    private static bool TryAbsoluteUri(string? value, out string address)
    {
        address = "";
        if (string.IsNullOrEmpty(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !uri.IsAbsoluteUri
            || !Uri.IsWellFormedUriString(value, UriKind.Absolute))
        {
            return false;
        }

        address = value;
        return true;
    }

    private readonly record struct Leaf(string KeyPath, string? Value);
}
