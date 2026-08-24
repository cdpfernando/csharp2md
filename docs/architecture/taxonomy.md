# Taxonomy

## Principles

- Machine names, IDs, kinds, facets and schemas use canonical English.
- Fact families, observations and relations are separate registries.
- Facets model independent axes; one flat exclusive `kind` must not mix structure, protocol, lifecycle and confidence.
- Names, prefixes and path similarity are never sufficient to promote causal knowledge.
- There is no generic `references` or `depends-on` relation in the causal graph.

## Fact families

| Family | Public types | Meaning |
| --- | --- | --- |
| `StructuralFact` | `Solution`, `Project`, `Document`, `Symbol` | Physical and semantic code inventory |
| `ArchitectureFact` | `Component`, `DeploymentUnit`, `EntryPoint`, `BoundaryOperation`, `ExternalSystem` | Runtime and architectural roles |
| `ContractFact` | `Contract`, `ContractBinding`, `ContractRevision` | Boundary payload identity, implementation and shape |
| `PersistenceFact` | `DataStore`, `DataObject`, `DataField`, `DataOperation` | Logical/physical persistence and runtime access |
| `ConfigurationFact` | `ConfigurationBinding` | Proven configuration structure without sensitive values |

`Controller`, `Handler`, `Repository`, `Client`, `Service` and similar roles are facets of symbols or architecture facts, not new top-level fact types.

## Observations

Every emitted observation has an owner, kind, normalized payload, evidence locator, extraction method, binding diagnostic, document hash and extractor version.

Always emitted when bindable:

- `Invocation`;
- `ObjectCreation`;
- `TypeUsage`;
- `BaseType`;
- `AttributeUsage`.

Emitted only in a registered context:

- `Assignment`;
- `Configuration`;
- `RouteDeclaration`;
- `MessageOperation`;
- `DataAccess`.

An observation identity uses owner, observation kind, normalized semantic payload and structural occurrence ordinal. Location is evidence, not identity.

## Architectural facets

### Boundary operations

```text
protocol:  http | grpc | messaging | cli | scheduler | function
direction: inbound | outbound
role:      command | query | event | stream | lifecycle
```

An entry point starts execution. A boundary operation crosses a boundary. One callable may participate in both roles; neither type substitutes for the other.

Outbound HTTP operation identity includes owner component, direction, protocol, client/destination scope, HTTP method and normalized route. Inbound identities use the owning component plus their protocol operation key.

### Persistence

```text
DataStore.technology:
  relational | document | key-value | cache | unknown

DataObject.form:
  table | view | collection | key-space | cache-region | unknown

DataOperation.operation:
  read | insert | update | delete | execute | unknown
```

CLR entity and property identities are logical. Explicit mapping can confirm physical table/column identity. Known convention produces a candidate mapping; missing context stays unresolved.

### Contracts

A contract exists only for a proven boundary payload. A CLR type alone is not a contract.

```text
Contract        = shared logical identity when a protocol/schema key is proven
ContractBinding = operation + payload role + CLR symbol + contract
ContractRevision = contract/binding scope + structural fingerprint
```

Protobuf full name, schema subject, formal specification, stable contract package or explicit configuration may prove shared identity. Structural or name similarity never merges contracts across owners.

## Canonical relations

Only the canonical direction is persisted. Inverse navigation is a derived posting.

| Relation | Valid semantic shape |
| --- | --- |
| `contains` | Structural owner contains structural child |
| `belongs-to` | Symbol belongs to component |
| `included-in` | Component is included in deployment unit |
| `executes` | Entry point executes callable |
| `invokes` | Callable invokes confirmed callable |
| `implements-operation` | Callable implements boundary operation |
| `targets` | Outbound boundary operation targets inbound operation, deployment or external system |
| `uses-contract` | Boundary operation uses contract with a registered payload role |
| `accesses-data` | Callable performs data operation |
| `operates-on` | Data operation targets data object or field |
| `maps-to` | CLR symbol maps through a closed mapping role |
| `configured-by` | Fact or operation is supported by proven configuration |

`maps-to.mapping_role` is closed:

- `contract-implementation`;
- `data-object-mapping`;
- `data-field-mapping`;
- `serialization-binding`.

A confirmed relation always has typed source and target, registered facets, `derived_from`, classifier ID/version and analysis variants. Its resolution is necessarily `confirmed`. Candidate and unresolved links are separate records, never edges in the confirmed graph. Observed text with no identity stays on an observation or open frontier; it never becomes a confirmed edge with a null target.

## Evidence dimensions

```text
evidence_method: semantic | syntactic | configured
resolution:      confirmed | candidate | unresolved
frontier:        closed | open
```

These dimensions describe different questions, but not every combination is valid for every record. `evidence_method` says how something was observed; `resolution` says whether an identity was closed; `frontier` says whether every statically demonstrable continuation at that occurrence was closed. A confirmed edge always has `resolution=confirmed`; an additional dynamic continuation is represented by a separate open frontier on the originating occurrence, not by weakening that edge.

The registry declares the minimum evidence method accepted by each promotion. Syntax may confirm declarations and reducible literals, but cannot confirm a causal target when alternatives remain.

Candidates never enter the confirmed graph. Consumers opt in to possible branches. There is no numeric confidence field: explanation comes from proof state, evidence and rejected candidates.

## Promotion

A classifier rule declares:

- required observations;
- accepted evidence methods;
- negative conditions;
- candidates considered and rejected;
- produced fact/relations and facets;
- classifier ID and version.

Conflicting classifiers do not use last-writer or execution-order precedence. The affected axis remains unconfirmed, the conflict is published, and certification fails when the conflict affects supported coverage.

Overrides may classify or associate existing identities and provide logical keys. They cannot invent code or contradict confirmed structural facts.

## Identity and versions

IDs never include absolute paths, source locations, translated labels or timestamps. Workspace identity scopes globally composable outputs; solution/project identities use logical relative paths. Moving a clone preserves IDs. Moving a project changes its default identity unless an explicit logical key preserves it.

Separate version axes are mandatory:

- `schema_version`: wire representation;
- `taxonomy_version`: meanings, types, facets and relation matrix;
- `observation_schema_version`: observable payload contract;
- `extractor_set_version`: extraction capabilities;
- `classifier_set_version`: promotion rules.

Additive changes preserve identities. A semantic identity change requires a new identity namespace or grammar.
