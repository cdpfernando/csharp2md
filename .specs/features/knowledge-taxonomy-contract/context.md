# Knowledge Taxonomy Contract Context

**Gathered:** 2026-08-24
**Spec:** `.specs/features/knowledge-taxonomy-contract/spec.md`
**Status:** Ready for design

---

## Feature Boundary

Workstream 1 of `architecture-knowledge-engine-roadmap.md`. Delivers `Csharp2Md.Domain` — the five fact families, the observation contract, the closed facet vocabularies, the twelve canonical relations, the three proof-state axes, the identity grammar and the five version axes — plus a committed machine-readable registry emitted from that domain and guarded by a drift gate. It creates no other assembly, removes no legacy code, and defines no wire schema or classifier rule.

---

## Implementation Decisions

### Registry authority direction

- The C# domain in `Csharp2Md.Domain` is hand-authored and is the single source of truth.
- A test-owned emitter, living outside the domain assembly, writes `contracts/taxonomy-registry.json`; the file is committed.
- A drift gate test compares the committed file to freshly emitted bytes and fails naming the differing entries.
- The emitter must live outside `Csharp2Md.Domain` because AD-006 forbids the domain from depending on JSON.
- Rejected: registry-first authoring, and two hand-authored artifacts cross-validated. Both allow the contract to be edited in two places.

### Legacy coexistence during this workstream

- `Csharp2Md.Domain` is added to `csharp2md.slnx` under the `src` folder, alongside legacy `Csharp2Md.Core`.
- No reference in either direction between the new domain and the legacy assembly.
- The whole solution must build green with `TreatWarningsAsErrors` for the duration of this feature.
- Legacy taxonomy deletion stays in workstream 2 `engine-bootstrap` and is not pulled forward.
- Rejected: leaving the project out of the solution. An unwired project is not covered by the build and would rot until workstream 2.

### Enforcement strength

- The domain rejects invalid combinations at construction, not at validation time downstream: unregistered `(source fact type, relation, target fact type)` triples, facet values outside a closed axis, missing required identity components, missing required observation metadata, and candidate or unresolved records entering the confirmed relation set.
- Illegal taxonomy states are unrepresentable rather than merely documented.
- Storage validation (workstream 3) and classifiers (workstreams 5A–5D) inherit this enforcement instead of reimplementing it.
- Rejected: exposing the matrix as queryable data only. That reproduces the legacy failure where each layer carried its own interpretation.

### Story slicing

- One atomic P1 covering the entire contract; no P2 or P3 tier.
- Rationale: seven downstream workstreams are blocked on this row, and a taxonomy missing families or relations unblocks none of them.

### Agent's Discretion

- Registry file path and JSON shape, given a stable key order that makes the drift gate a byte comparison.
- Test project name and layout, given it is a new project rather than an addition to the legacy test project.
- Whether construction rejection surfaces as an exception or a result type — deferred to `design.md`.
- Internal representation of the relation matrix and the facet axes.

### Declined / Undiscussed Gray Areas → Assumptions

Every remaining decision is logged in the spec's Assumptions & Open Questions table with a chosen default and rationale: registry location and format, test project placement, initial version-axis values, how rejection is surfaced, determinism evidence, the meaning of "exactly" in vocabulary criteria, and the `contains` relation scope.

---

## Specific References

- `docs/architecture/taxonomy.md` is the normative source for every vocabulary, relation shape, identity rule and version axis in the spec. The spec restates them as testable criteria; it does not extend them.
- `docs/architecture/quality-and-security.md` is the source for the literal allowlist and the suspected-secret evidence shape.
- AD-001, AD-004, AD-006 and AD-010 in `.specs/STATE.md` are the decisions this contract makes executable.

---

## Deferred Ideas

- None — discussion stayed within the workstream 1 boundary. Everything raised and excluded is already attributed to a later workstream in the spec's Out of Scope table.
