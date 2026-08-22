# Component Graph Specification

## Problem Statement

`raw/dependencies.mmd` is dead output. A live run over `fixtures/SyntheticSolution` writes exactly
`"flowchart LR\n"` — zero nodes, zero edges — even though the same run resolves 15 cross-project relations
and 11 relations into proven database objects. AD-020 recorded why and deliberately left it unfixed: no
production code path anywhere constructs a `ComponentFact` (the only `new ComponentFact(` in the repository
is in a test), and separately `RelationProjector.Mermaid` keys its `componentByProject` lookup by
project-shaped `FactId`s while every relation's `SourceId`/`TargetId` is symbol-, document- or
database-shaped, so the lookup would miss even if components existed. Both gaps must close together before
the diagram can carry a single honest edge.

## Goals

- [ ] Every analysed project becomes exactly one persisted, proven `ComponentFact`.
- [ ] `raw/dependencies.mmd` over `fixtures/SyntheticSolution` contains at least one edge outside the
      `data` partition and at least one edge into a database object node.
- [ ] Edge count in the diagram is bounded by distinct (source, target, partition, kind) groups, not by
      the number of underlying relations.
- [ ] AD-020 is superseded — the Success Criterion it waived becomes met.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| A factual schema version bump | `schemas/facts.schema.json` already declares `components` in `required` and `fact_kind` already admits `"component"`. The wire contract needs no change, so `FactualJsonSerializer.SchemaVersion` stays at 5. |
| Service-level (solution-scoped) components | Deferred to P2. Needs a shared-project ownership rule first: `Acme.Shared.Contracts` belongs to both `Acme.Orders.slnx` and `Acme.Payments.slnx`, which trips `RelationProjector`'s "belongs to more than one component index entry" invariant. |
| Placeholder nodes for unresolved relations | Deferred to P3. The 60 unresolved relations in the fixture would dominate the diagram; they stay fully visible in `raw/facts/relations/*.json`. |
| Wiring or retiring the orphaned `Detection/` tree | AD-015's open question, owned by the `detection-tree-revival` feature. This feature produces components from inventory, not from detectors. |
| Changing how any relation resolves | `relation-resolver` owns resolution. This feature only projects relations that already carry a target. |
| Per-component or per-module Markdown wiki pages | Roadmap item marked ADIADO; it depends on this feature but is not part of it. |
| Reviving `raw/dependencies.json` | Removed by AD-010. Relations live in `raw/facts/relations/*.json`. |
| A `publishes` candidate-selection fix | Owned by `relation-resolver-candidate-fix`. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here — nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Component granularity | One component per analysed project, `ComponentKind` `"project"` | User chose it over service granularity (AskUserQuestion, 2026-08-22). Service granularity additionally trips the one-project-two-components invariant on this repository's own fixture. | y |
| Edge collapsing | One edge per (source, target, partition, kind), labelled with the collapsed count | User chose it over one-edge-per-relation and one-edge-per-pair (AskUserQuestion, 2026-08-22). Keeps kind information while bounding diagram size. | y |
| Non-project nodes | Database objects are nodes; nothing else is | User chose "Projects + database objects" (AskUserQuestion, 2026-08-22). | y |
| Relations inside one component | Omitted from the diagram | At project granularity most `calls`/`creates`/`references` are intra-project (17 of the fixture's 32 resolved relations); rendering them as self-loops would swamp the inter-component signal they exist to show. The detail stays in `raw/facts/relations/*.json`. | n |
| Column-targeted relations | Rendered against the owning `DatabaseObjectFact` node, never a node of their own | `DatabaseColumnFact` carries `ObjectId`, so the roll-up is exact, not inferred. A node per column would put 6 of the fixture's columns on the diagram for no architectural signal. | n |
| Database objects are not `ComponentFact`s | Projected directly from `DatabaseObjectFact`s the projector already receives | `ComponentFact.ProjectIds` is `ImmutableArray<ProjectFactId>` and `ComponentFactId.Create` throws on an empty owner set, so a non-project owner is inexpressible without the schema bump this spec puts out of scope. | n |
| Component fact evidence and resolution | No evidence; resolution mirrors the owning `ProjectFact`'s | A `.csproj` is not a `DocumentFact`, so no `Evidence` line range exists to point at, and `FactValidator` demands evidence only for relations. Mirroring the project's resolution avoids claiming more than the project fact itself does — a live run shows every project at `syntactic` in the default syntax-only mode, so a hardcoded `Exact` would have been a lie. | n |
| Node labels | Project node labels use `ProjectFact.Name`; database node labels use `DatabaseObjectFact.Name` | A raw component id renders as `id1:component;kind=project;owners=id1%3Aproject%3Bpath%3D...`, which is unreadable in a diagram. Both names are already in facts the pipeline holds at the projection call site. | n |
| `raw/codebase/components.md` contents | Project components only | Database objects already have `raw/facts/database.json` as their catalogue; listing them in a file named after `ComponentFact` would misrepresent what a component is. | n |
| Unresolved relations | Stay out of the diagram | Already the projector's behaviour (`TargetId is not null` filter) and consistent with AD-011: an unproven target is not a graph node. | n |

**Open questions:** none — all resolved or logged above.

---

## User Stories

### P1: Every analysed project is a proven component ⭐ MVP

**User Story**: As an engineer reverse-engineering a codebase, I want each project to exist as a first-class
component fact so that the graph, the component index, and any future component page all hang off one
identity rather than being re-derived per consumer.

**Why P1**: Nothing downstream can render a component that was never produced. This is the first of AD-020's
two blockers.

**Acceptance Criteria**:

1. WHEN the analysis finishes its document loop THEN the system SHALL emit exactly one `ComponentFact` per
   `ProjectFact` the run produced, with `ComponentKind` `"project"` and `ProjectIds` holding that project's
   `ProjectFactId` and no other.
2. The system SHALL persist every emitted `ComponentFact` in one solution-level factual fragment under
   `raw/facts/`, reachable from `raw/facts/manifest.json`.
3. WHEN one project is reached through more than one solution path THEN the system SHALL emit one
   `ComponentFact` for it rather than one per path.
4. The system SHALL give every emitted `ComponentFact` a header whose `Resolution` equals its project's
   `ProjectFact` header `Resolution`, at least one `FactProvenance` entry naming the component producer,
   and an empty `Evidence` array.
5. WHEN components are emitted THEN `raw/codebase/components.md` SHALL list every one of them ordered by
   component id, each with its component kind and its project ids.
6. IF the run analysed zero projects THEN the system SHALL emit no `ComponentFact` and SHALL still write
   `raw/dependencies.mmd` as `flowchart LR` and `raw/codebase/components.md` as `# Components`.
7. The system SHALL leave `FactualJsonSerializer.SchemaVersion` at 5.
8. IF two emitted `ComponentFact`s share one component identity THEN the system SHALL fail the run with a
   structural error rather than keeping either silently.
9. IF one project id maps to more than one component THEN the system SHALL fail the run with a structural
   error.

**Independent Test**: Run the CLI over `fixtures/SyntheticSolution` and follow `raw/facts/manifest.json` to
the persisted fragment: it holds 5 `ComponentFact`s, one per `ProjectFact` the run produced —
`Acme.Orders`, `Acme.Payments`, `Acme.Shared.Contracts`, `Acme.Broken` and `Acme.DoesNotExist` — each with
`resolution: "syntactic"` mirroring its project in the default syntax-only mode, and each with exactly one
project id. `raw/codebase/components.md` lists all 5.

---

### P1: The diagram shows real, deduped edges between components ⭐ MVP

**User Story**: As an engineer, I want `raw/dependencies.mmd` to show which components actually depend on
which so that I can see the system's shape without reading 32 relation records by hand.

**Why P1**: This is the criterion the roadmap names and the second of AD-020's two blockers. Without it the
components from the previous story are invisible.

**Acceptance Criteria**:

1. WHEN a relation has a non-null target AND its source and target map to two different nodes THEN
   `raw/dependencies.mmd` SHALL contain an edge from the source node to the target node.
2. WHEN two or more relations share one (source node, target node, partition, relation kind) group THEN the
   system SHALL emit exactly one edge line for that group, labelled `<partition>:<kind> ×<count>` where
   `<count>` is how many relations the group collapsed.
3. WHEN a relation's source and target map to the same node THEN the system SHALL omit that relation from
   the diagram.
4. IF a relation's `TargetId` is null THEN the system SHALL omit that relation from the diagram.
5. IF a relation's source or target maps to neither a component nor a database object THEN the system SHALL
   omit that relation from the diagram.
6. The system SHALL emit a node line for every node touched by at least one emitted edge, and no node line
   for a node no emitted edge touches.
7. WHEN rendering a project component node THEN the system SHALL label it with the project's
   `ProjectFact.Name`, not with a fact id.
8. The system SHALL order node lines by node label ordinal and edge lines by (source node label, target node
   label, partition, relation kind) ordinal, so two runs over identical input write a byte-identical
   `raw/dependencies.mmd`.
9. WHEN a node or edge label contains `#`, `"`, `|`, a carriage return, or a line feed THEN the system SHALL
   emit `#35;`, `#quot;`, `#124;`, nothing, and a single space respectively.
10. The system SHALL emit at most one edge line per distinct (source node, target node, partition, relation
    kind) group regardless of how many relations the run resolved.

**Independent Test**: Run the CLI over `fixtures/SyntheticSolution` and read `raw/dependencies.mmd`. It
contains an `Acme.Orders → Acme.Shared.Contracts` edge labelled `structural:calls ×5`, plus edges for
`structural:references` (Orders→Shared.Contracts and Payments→Shared.Contracts), `events:publishes`
(Orders→Shared.Contracts and Payments→Shared.Contracts), `events:handles` and `events:subscribes`
(Payments→Shared.Contracts) — 7 edge lines outside the `data` partition. No self-edge appears, and running
twice produces identical bytes.

---

### P1: Database objects appear as graph nodes ⭐ MVP

**User Story**: As an engineer, I want the tables, views and procedures the code touches to appear in the
diagram so that I can see which component owns which persistence without opening
`raw/facts/database.json`.

**Why P1**: The user selected database objects as in-scope nodes. The `data` partition is where the fixture
has the most proven targets (11 resolved cross-owner relations), so excluding it would leave the richest
part of the analysis invisible.

**Acceptance Criteria**:

1. WHEN a resolved relation targets a `DatabaseObjectFact` THEN the diagram SHALL contain a node for that
   object and an edge from the source component's node to it.
2. WHEN a resolved relation targets a `DatabaseColumnFact` THEN the system SHALL render the edge against
   that column's owning `DatabaseObjectFact` node and SHALL NOT emit a node for the column.
3. WHILE column-targeted relations are rolled up onto their owning object, the system SHALL fold them into
   the same (source, target, partition, kind) dedupe group as any other relation reaching that object.
4. The system SHALL label a database object node with the object's `Name` and render it with Mermaid's
   cylinder shape `[("…")]`, distinct from a project component's rectangle `["…"]`.
5. The system SHALL NOT emit a `ComponentFact` for any database object, and `raw/codebase/components.md`
   SHALL contain no database object.

**Independent Test**: Run the CLI over `fixtures/SyntheticSolution` and read `raw/dependencies.mmd`. It
contains cylinder nodes for `order_headers`, `Orders` and `usp_RebuildOrderTotals`, each reached by an edge
from the `Acme.Orders` rectangle, including a `data:writes-column ×4` edge into `Orders`. No node named
after a column appears, and `raw/codebase/components.md` mentions none of the three.

---

### P1: Omission is bounded and observable ⭐ MVP

**User Story**: As an engineer, I want to know when the diagram is hiding something so that I do not read an
incomplete picture as a complete one.

**Why P1**: Every rule in the two stories above drops relations. Silent dropping is what made the current
empty diagram look like an empty codebase for four features running.

**Acceptance Criteria**:

1. WHEN one or more resolved relations are omitted because an endpoint mapped to no node THEN the system
   SHALL record exactly one summary diagnostic reporting how many were omitted, not one per relation.
2. The system SHALL NOT record a diagnostic for a relation omitted because it is a self-edge or because its
   target is null, since both are specified behaviour rather than a gap.
3. WHEN the summary diagnostic is recorded THEN it SHALL reach `raw/facts/diagnostics.json` alongside every
   other analysis diagnostic.

**Independent Test**: Run the CLI over a fixture whose relations all resolve within known nodes and confirm
no omission diagnostic is written; then run over `fixtures/SyntheticSolution` and confirm the count in the
single omission diagnostic matches the number of resolved relations whose endpoints are not nodes.

---

### P2: Service-level component rollup

**User Story**: As an engineer, I want components at solution/service granularity so that the diagram
matches how the system is deployed rather than how it is compiled.

**Why P2**: Useful for the eventual wiki, but it requires a shared-project ownership rule that the fixture
already forces (`Acme.Shared.Contracts` sits in two solutions) and the project-level graph delivers the
roadmap criterion without it.

**Acceptance Criteria**:

1. WHERE service-level components are enabled the system SHALL emit one `ComponentFact` per inventory
   service with `ComponentKind` `"service"` and every member project's id in `ProjectIds`.
2. IF a project belongs to more than one service THEN the system SHALL resolve it to exactly one owning
   component by a documented rule rather than failing the run.

**Independent Test**: Run over `fixtures/SyntheticSolution` with service components enabled and confirm two
service nodes appear with `Acme.Shared.Contracts` attributed to exactly one of them.

---

### P3: Placeholder nodes for unresolved endpoints

**User Story**: As an engineer, I want to see that the analysis observed a dependency it could not prove so
that the diagram distinguishes "no dependency" from "unproven dependency".

**Why P3**: Honest but noisy — the fixture has 60 unresolved relations against 32 resolved ones.

**Acceptance Criteria**:

1. WHERE placeholder nodes are enabled, WHEN a relation has a null target and a recorded observed target
   text THEN the system SHALL render a distinctly shaped node labelled with that text and an edge into it.

---

## Edge Cases

- IF a project produced no resolved cross-component relation THEN the system SHALL still emit its
  `ComponentFact` and list it in `raw/codebase/components.md`, while omitting it from
  `raw/dependencies.mmd` (no edge touches it).
- IF a project produced a `ProjectFact` but no documents because its file could not be opened
  (`Acme.DoesNotExist`) THEN the system SHALL still emit its `ComponentFact`, mirroring the project fact
  one-for-one, and the component SHALL appear in `raw/codebase/components.md` while never appearing in
  `raw/dependencies.mmd`.
- WHEN two database objects share a name across different connections THEN the system SHALL keep them as
  two nodes distinguished by their `DatabaseObjectFactId`, not collapse them by label.
- IF a `ComponentFact` references a `ProjectFactId` that the run never produced THEN the system SHALL fail
  validation with `C2M-FV-002`.
- WHEN the diagram contains no edges at all THEN the system SHALL write `raw/dependencies.mmd` as exactly
  `flowchart LR` with no node lines.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| COMP-01 | P1: Every analysed project is a proven component | Design | Pending |
| COMP-02 | P1: Every analysed project is a proven component | Design | Pending |
| COMP-03 | P1: Every analysed project is a proven component | Design | Pending |
| COMP-04 | P1: Every analysed project is a proven component | Design | Pending |
| COMP-05 | P1: Every analysed project is a proven component | Design | Pending |
| COMP-06 | P1: Every analysed project is a proven component | Design | Pending |
| COMP-07 | P1: Every analysed project is a proven component | Design | Pending |
| COMP-08 | P1: Every analysed project is a proven component | Design | Pending |
| COMP-09 | P1: Every analysed project is a proven component | Design | Pending |
| COMP-10 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-11 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-12 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-13 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-14 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-15 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-16 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-17 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-18 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-19 | P1: The diagram shows real, deduped edges | Design | Pending |
| COMP-20 | P1: Database objects appear as graph nodes | Design | Pending |
| COMP-21 | P1: Database objects appear as graph nodes | Design | Pending |
| COMP-22 | P1: Database objects appear as graph nodes | Design | Pending |
| COMP-23 | P1: Database objects appear as graph nodes | Design | Pending |
| COMP-24 | P1: Database objects appear as graph nodes | Design | Pending |
| COMP-25 | P1: Omission is bounded and observable | Design | Pending |
| COMP-26 | P1: Omission is bounded and observable | Design | Pending |
| COMP-27 | P1: Omission is bounded and observable | Design | Pending |
| COMP-28 | Edge case: project with no cross-component relation | Design | Pending |
| COMP-29 | Edge case: project fact with no documents | Design | Pending |
| COMP-30 | Edge case: same-named objects on different connections | Design | Pending |
| COMP-31 | Edge case: component referencing an unknown project id | Design | Pending |
| COMP-32 | Edge case: diagram with no edges | Design | Pending |
| COMP-40 | P2: Service-level component rollup | - | Pending |
| COMP-41 | P2: Service-level component rollup | - | Pending |
| COMP-50 | P3: Placeholder nodes for unresolved endpoints | - | Pending |

**ID mapping**: `COMP-01`..`COMP-09` are P1 story 1's ACs 1-9 in order; `COMP-10`..`COMP-19` are P1 story
2's ACs 1-10; `COMP-20`..`COMP-24` are P1 story 3's ACs 1-5; `COMP-25`..`COMP-27` are P1 story 4's ACs 1-3;
`COMP-28`..`COMP-32` are the Edge Cases in order; `COMP-40`/`COMP-41` are P2's ACs; `COMP-50` is P3's AC.

**Coverage:** 35 total, 32 in P1 scope for this task list, 3 (P2/P3) deferred.

---

## Success Criteria

- [ ] A CLI run over `fixtures/SyntheticSolution` writes a `raw/dependencies.mmd` containing at least one
      edge outside the `data` partition — the criterion AD-020 waived.
- [ ] The same file contains at least one edge into a database object node.
- [ ] The same file contains no self-edge and no node untouched by an edge.
- [ ] Two consecutive runs over identical input produce byte-identical `raw/dependencies.mmd`.
- [ ] `raw/codebase/components.md` lists one component per analysed project and nothing else.
- [ ] `FactualJsonSerializer.SchemaVersion` is still 5 and `schemas/facts.schema.json` is unmodified.
- [ ] A new decision record supersedes AD-020.
