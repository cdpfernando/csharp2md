using Csharp2Md.Core.PackageBuilding;

namespace Csharp2Md.Core.Publication;

internal sealed record PackageValidationFailure(string Code, string Stage, string Cause, string? Family, string? Artifact);

internal sealed record PackageValidationReport(ImmutableArray<PackageValidationFailure> Failures)
{
    internal bool Succeeded => Failures.IsDefaultOrEmpty;
}

internal static class PackageValidator
{
    internal static PackageValidationReport Validate(string packageDirectory)
    {
        try
        {
            using var reader = PackageReader.Open(packageDirectory);
            return Validate(reader);
        }
        catch (FileNotFoundException)
        {
            return Failure("package-corruption", "validation", "missing-artifact", "manifest.json");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            return Failure("package-corruption", "validation", "invalid-package", "manifest.json");
        }
    }

    internal static PackageValidationReport Validate(PackageReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        try
        {
            var artifacts = reader.ReadDeclaredArtifacts();
            foreach (var artifact in artifacts)
            {
                if (System.Text.Encoding.UTF8.GetString(artifact.Value.AsSpan()).Contains("C:/", StringComparison.Ordinal))
                {
                    return Failure("publication-safety", "validation", "absolute-path", artifact.Key);
                }
            }
            _ = RetrievalModelReader.Read(artifacts);
            RetrievalModelReader.VerifyMarkdown(artifacts);
            _ = CanonicalJson.Read<PackageCertification>(artifacts["certification.json"].AsSpan());
            _ = CanonicalJson.Read<PublicationMeasurements>(artifacts["measurements.json"].AsSpan());
            return new PackageValidationReport([]);
        }
        catch (PackageCorruptionException exception)
        {
            return Failure("package-corruption", "validation", "invalid-artifact", exception.Artifact);
        }
        catch (FileNotFoundException exception)
        {
            var artifact = exception.FileName is null ? "manifest.json" : Path.GetRelativePath(reader.PackageDirectory, exception.FileName).Replace('\\', '/');
            return Failure("package-corruption", "validation", "missing-artifact", artifact);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            return Failure("package-corruption", "validation", "invalid-package", "manifest.json");
        }
    }

    private static PackageValidationReport Failure(string code, string stage, string cause, string artifact) =>
        new([new PackageValidationFailure(code, stage, cause, FamilyFor(artifact), artifact)]);

    private static string? FamilyFor(string artifact) => artifact switch
    {
        "manifest.json" => "manifest",
        _ when artifact.Contains("/indexes/", StringComparison.Ordinal) => "index",
        _ when artifact.Contains("/markdown/", StringComparison.Ordinal) => "markdown",
        _ when artifact.Contains("/graph/", StringComparison.Ordinal) => "graph",
        _ when artifact.Contains("/tables/", StringComparison.Ordinal) => "table",
        _ => null,
    };
}
