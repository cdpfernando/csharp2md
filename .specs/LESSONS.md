# LESSONS - auto-maintained by scripts/lessons.py

> Machine-owned. Do NOT hand-edit. Changes are overwritten on the next `lessons.py` write.
> Canonical state lives in `.specs/lessons.json`. Edit lessons only via the script.
> promote_threshold=2 distinct features · window_days=45 · quarantine_threshold=2

## Confirmed (load these at Specify/Design)

Corroborated across multiple features. Safe to apply as guidance.

_none_

## Candidates (under observation - do NOT load as guidance yet)

Seen once or not yet corroborated. Tracked, not trusted.

### L-001 - When a construction-time guard needs data (a materialized fact or an evidence method) that the approved factory signature does not carry, wire it in via an explicit signature parameter or document it as a mandatory pre-call for callers -- do not ship it as a standalone public guard whose only call site is its own test.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · harmful: 0
- features: knowledge-taxonomy-contract
- evidence: TAX-46,TAX-50,TAX-51,TAX-53 / src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs:46-83, RelationShapeGuards.cs:18-101
- last seen: 2026-08-25T10:32:02Z

### L-002 - When a boundary AC says any production project, enumerate every production assembly in the theory, not a subset closed by a sibling requirement.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `isolation` · harmful: 0
- features: engine-bootstrap
- evidence: ENG-06 (isolation)
- last seen: 2026-08-25T15:35:07Z

### L-003 - When an AC names the process temp directory, snapshot it or record an approved design mitigation; a working-tree snapshot alone does not cover that clause.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `pipeline` · harmful: 0
- features: engine-bootstrap
- evidence: ENG-16 (pipeline)
- last seen: 2026-08-25T15:35:37Z

### L-004 - When an AC requires observations from bindable occurrences after compile errors, run extraction on that tree and assert a bound observation, not only a diagnostic and a kept compilation.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `extraction` · harmful: 0
- features: roslyn-observation-extraction
- evidence: ROSE-23 (extraction)
- last seen: 2026-08-26T03:25:39Z

### L-005 - When an AC requires commit abort on identity collision, assert Unpublished plus a named identity and return the accumulator corruption flag as the stage result the orchestrator reads.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `pipeline` · harmful: 0
- features: roslyn-observation-extraction
- evidence: ROSE-21 (pipeline)
- last seen: 2026-08-26T03:26:08Z

### L-006 - Do not specify empty-string StructuralLiteral outcomes; Domain TAX-80 rejects empty canonical text
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `domain-literals` · harmful: 0
- features: entrypoints-boundaries-contracts
- evidence: spec.md EBC-06 empty-template edge; BoundaryPassTests.cs:106; StructuralLiteralTests.cs:66 (domain-literals)
- last seen: 2026-08-26T06:33:57Z

### L-007 - When a spec outcome is unrepresentable in Domain, record SPEC_DEVIATION and assert the Domain-feasible diagnostic instead of inventing an empty literal
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `analysis-classification` · harmful: 0
- features: entrypoints-boundaries-contracts
- evidence: BoundaryPass.cs:85 SPEC_DEVIATION EBC-06 (analysis-classification)
- last seen: 2026-08-26T06:33:57Z

### L-008 - When two spec IF-conditions collapse to one ledger payload shape, assert each remaining distinguishable shape's UnresolvedRecord kind and cause.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `analysis-classification` · harmful: 0
- features: persistence-knowledge
- evidence: PK-39/PK-40 PersistenceModelBuilder.cs:84 (analysis-classification)
- last seen: 2026-08-26T16:56:36Z

### L-009 - When a spec WHEN is multi-TFM uniqueness, assert two target frameworks or analysis variants, not emitter re-entry on a single-TFM ledger.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · harmful: 0
- features: components-deployments-configuration
- evidence: CDC-04
- last seen: 2026-08-26T21:45:33Z

### L-010 - A diagnostic that must name both the referencing project and the missing target must assert both identities, not only the missing path.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · harmful: 0
- features: components-deployments-configuration
- evidence: CDC-07
- last seen: 2026-08-26T21:45:46Z

### L-011 - When an Independent Test names a published envelope file, assert those records on that file after a fixture analyze, not only on a hand-built snapshot.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · harmful: 0
- features: components-deployments-configuration
- evidence: P2 Independent Test CDC-56/CDC-57
- last seen: 2026-08-26T21:45:54Z

### L-012 - When an AC requires a runtime sequence after X before Y, assert that order through Commit observables, not only IndexOf on the pipeline source.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `publication` · harmful: 0
- features: retrieval-projections
- evidence: RP-02 ProjectorPublicationTests.cs:81-89 (publication)
- last seen: 2026-08-27T03:31:59Z

### L-013 - When an AC says exactly one artifact under a prefix, either keep companion metadata out of that prefix or name the companion in the spec.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `source-projection` · harmful: 0
- features: retrieval-projections
- evidence: RP-07 RedactionEnvelopeTests.cs:26-35 (source-projection)
- last seen: 2026-08-27T03:31:59Z

### L-014 - When an AC names a published shard file, assert the hash by reading that file, not only the in-memory DTO that feeds it.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `source-projection` · harmful: 0
- features: retrieval-projections
- evidence: RP-11 SourceIntegrityTests.cs:29 (source-projection)
- last seen: 2026-08-27T03:31:59Z

### L-015 - When an Independent Test says slice a published source artifact, assert against that artifact, not the original clone file.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `locators` · harmful: 0
- features: retrieval-projections
- evidence: RP-15 Independent Test SymbolFactEmitterTests.cs:309-320 (locators)
- last seen: 2026-08-27T03:31:59Z

### L-016 - When projection validation must cover Markdown pages and source locators, drive Commit on the published citation form, not only a JSON stand-in.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `projection-validation` · harmful: 0
- features: retrieval-projections
- evidence: RP-41/RP-44/RP-45 Independent Test (projection-validation)
- last seen: 2026-08-27T03:32:00Z

### L-017 - When a spec edge case treats a contribution as unpublished because its package directory is missing, assert that batch-manifest.json status, not only that the accumulator stayed empty at commit time.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `batch-publication` · harmful: 0
- features: multi-solution-composition
- evidence: MSC-39 (batch-publication)
- last seen: 2026-08-27T12:20:19Z

### L-018 - When an AC requires ordering a list of entries by identity, assert that order with at least two distinct identities, not only owner order inside a single entry.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `composition` · harmful: 0
- features: multi-solution-composition
- evidence: MSC-27 (composition)
- last seen: 2026-08-27T12:20:29Z

### L-019 - When an Independent Test names a fixture scenario, cover that fixture; a synthetic unit grouping is not the same evidence.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `composition` · harmful: 0
- features: multi-solution-composition
- evidence: MSC-32 Independent Test (composition)
- last seen: 2026-08-27T12:20:39Z

### L-020 - Never exclude an artifact from an invariant check to make it pass; if an artifact cannot satisfy the invariant, that is a gap to report, not a list to skip
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `storage` · harmful: 0
- features: generator-cli-projections-certification
- evidence: tests/Csharp2Md.Storage.Tests/Scale/ScaleInputGenerator.cs:219 (storage) (+1 more)
- last seen: 2026-09-11T16:35:08Z

### L-021 - A test that hand-builds the record the producer is supposed to emit proves the consumer, never the producer; assert the producing layer emits it end to end
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `analysis` · harmful: 0
- features: generator-cli-projections-certification
- evidence: tests/Csharp2Md.Projection.Tests/Postings/MessagingContractPostingTests.cs:75 (analysis)
- last seen: 2026-09-11T01:30:25Z

### L-022 - A ground-truth resolver must read the discrete published record the requirement names, never infer the outcome from a sibling artifact that is present either way
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `certification` · harmful: 0
- features: generator-cli-projections-certification
- evidence: tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationRunner.cs:161 (certification)
- last seen: 2026-09-11T01:30:25Z

### L-023 - An envelope field only round-tripped in a serialization test is unproven until some real pipeline path populates it
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `storage` · harmful: 0
- features: generator-cli-projections-certification
- evidence: GCPC-004 (storage)
- last seen: 2026-09-11T01:30:25Z

### L-024 - Assert a CLI verb's own exit code by running that verb, not a sibling verb reading a rewritten artifact
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `cli` · harmful: 0
- features: generator-cli-projections-certification
- evidence: validation.md GCPC-069/GCPC-070; src/Csharp2Md.Cli/CommandFactory.cs:147 (cli)
- last seen: 2026-09-11T16:35:08Z

### L-025 - Publish an artifact's real byte size even when a deferred writer owns its payload; a zero placeholder is a false cardinality claim
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `storage` · harmful: 0
- features: generator-cli-projections-certification
- evidence: validation.md GCPC-061; src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs:46 (storage)
- last seen: 2026-09-11T16:35:08Z

### L-026 - When a version axis advances, update every place the package republishes it; two copies with different values make the package self-contradicting
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `storage` · harmful: 0
- features: generator-cli-projections-certification
- evidence: validation.md GCPC-057; src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs:65 (storage)
- last seen: 2026-09-11T16:35:08Z

### L-027 - Route a degradation reason from the family where the degradation actually occurs, not only from families that are convenient to attribute
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `storage` · harmful: 0
- features: generator-cli-projections-certification
- evidence: validation.md GCPC-004 edge case; src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs:234 (storage)
- last seen: 2026-09-11T16:35:08Z

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
