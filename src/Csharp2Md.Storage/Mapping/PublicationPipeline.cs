using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Retrieval;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal sealed record PublicationOutcome(
    ImmutableArray<StagedFragment> Fragments,
    SolutionContribution? Contribution);

internal static class PublicationPipeline
{
    internal static PublicationOutcome Publish(
        FactualSnapshot snapshot,
        ManifestContext context,
        SolutionCoordinate coordinate,
        string packageDirectory,
        IPackageProjector? projector,
        IBatchComposer? composer,
        ISourceDocumentReader source,
        Func<WireDocument, PublishedPackageView>? createView = null,
        int? readingBudgetTokens = null,
        int? maxFileReadsPerScenario = null,
        ImmutableArray<string> allowlist = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        var document = DomainMapper.ToWire(snapshot, context);
        var report = PackageValidator.Validate(document);

        // T52: the derived ceiling (CeilingCalculator.Derive's defaults, absent a CLI override) is now the
        // enforced default for every live publication -- closing the T37 deferred item. Every existing
        // caller that never supplied a budget keeps getting the same ~32 KiB default the CLI itself falls
        // back to; only a caller that explicitly passes readingBudgetTokens/maxFileReadsPerScenario moves
        // it.
        var ceiling = CeilingCalculator.Derive(
            readingBudgetTokens ?? CeilingCalculator.DefaultReadingBudgetTokens,
            maxFileReadsPerScenario ?? CeilingCalculator.DefaultMaxFileReadsPerScenario);
        var allowlistDigest = ProvenanceDto.ComputeAllowlistDigest(allowlist.IsDefault ? [] : allowlist);
        var provenance = ProvenanceDto.Current(ceiling, allowlistDigest);

        var plan = LayoutPlanner.Plan(report.Document, ceiling.CeilingBytes);
        var projections = ImmutableArray<StagedFragment>.Empty;
        SolutionContribution? contribution = null;
        var publishedDocument = report.Document;
        if (projector is not null || composer is not null)
        {
            var view = createView is null
                ? PublishedPackageView.From(report.Document, plan)
                : createView(report.Document);
            if (projector is not null)
            {
                try
                {
                    projections = projector.Project(view, source);
                }
                catch (PublicationRejectedException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new PublicationRejectedException("projection", exception.Message, exception);
                }

                ProjectionValidator.Validate(view, projections);

                // Deferred item (context.md, found at T47): a real `retrieval.md` (the real PackageProjector
                // -- not every projector test double emits one) means GCPC-052..GCPC-054's documented
                // scenarios can actually be walked against exactly what this publication is about to write,
                // so their measured records reach a real `measurements.json` instead of staying proven only
                // in the runner's own tests. Gated on the guide's presence so every existing caller with a
                // projector that has no retrieval.md (unit-test doubles across Storage/Projection) is
                // unaffected -- this only ever activates for the real, end-to-end `analyze` pipeline.
                if (projections.Any(static fragment => fragment.CanonicalKey == "retrieval.md"))
                {
                    var candidateFragments = PackagePublisher.ToPublicationOrder(report.Document, plan, projections, provenance);
                    var scenarioRecords = RetrievalScenarioRunner
                        .Run(new StagedFragmentArtifactSource(candidateFragments))
                        .ToMeasurementRecords();
                    if (!scenarioRecords.IsEmpty)
                    {
                        publishedDocument = report.Document with
                        {
                            Measurements = new MeasurementsEnvelope(
                                report.Document.Measurements.Records.AddRange(scenarioRecords)),
                        };

                        // Re-plan: the fact/relation families are unchanged (measurements.json carries no
                        // fact or relation identity, so it never participates in their sharding), only
                        // measurements.json's own declared record count moves. The already-built `view`
                        // keeps flowing to `composer.Contribute` below unchanged, so a caller depending on
                        // exactly one `createView` invocation per publish (a same-instance-to-projector-
                        // and-composer guarantee) still sees exactly that.
                        plan = LayoutPlanner.Plan(publishedDocument, ceiling.CeilingBytes);
                    }
                }
            }

            if (composer is not null)
            {
                contribution = composer.Contribute(view, coordinate, packageDirectory);
            }
        }

        // F4 (GCPC-004): a layout-time degradation (LayoutPlanner's `record-exceeds-ceiling`, e.g. a
        // solitary oversized `invokes`/`accesses-data`/`uses-contract` relation) is computed only once
        // `plan` exists, after the coverage envelope DomainMapper.ToWire already baked from the analysis
        // snapshot -- so it is merged onto the document actually serialized here, right before ordering,
        // rather than staying silently unread as it did before this fix. Applied last so it reflects
        // whichever `plan` (original or the retrieval-scenario re-plan above) is about to be written.
        if (!plan.CoverageMetricDegradations.IsEmpty)
        {
            publishedDocument = publishedDocument with
            {
                Coverage = DomainMapper.WithCoverageDegradations(publishedDocument.Coverage, plan.CoverageMetricDegradations),
            };
        }

        var fragments = PackagePublisher.ToPublicationOrder(publishedDocument, plan, projections, provenance);
        ValidateManifestCardinality(fragments);
        return new PublicationOutcome(fragments, contribution);
    }

    /// <summary>
    /// Proves the manifest this publication is about to write agrees with the bytes it is about to write,
    /// before any of them reach disk (GCPC-061/GCPC-062) -- an abort here leaves the prior package
    /// untouched, since nothing has been written yet.
    /// </summary>
    private static void ValidateManifestCardinality(ImmutableArray<StagedFragment> fragments)
    {
        var manifestFragment = fragments.Single(static fragment => fragment.CanonicalKey == PackagePublisher.ManifestKey);
        var manifest = PackageValidator.ReadPayloadOrThrow<ManifestEnvelope>(
            manifestFragment.Payload.AsSpan(), PackagePublisher.ManifestKey);

        var artifactsByKey = new Dictionary<string, ImmutableArray<byte>>(StringComparer.Ordinal);
        var deferredKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fragment in fragments)
        {
            if (fragment.Role != ArtifactRole.Payload)
            {
                continue;
            }

            if (fragment.IsDeferred)
            {
                deferredKeys.Add(fragment.CanonicalKey);
            }
            else
            {
                artifactsByKey[fragment.CanonicalKey] = fragment.Payload;
            }
        }

        PackageValidator.ValidatePublishedManifest(manifest, artifactsByKey, deferredKeys);
    }
}
