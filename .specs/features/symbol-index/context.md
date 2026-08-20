# SymbolIndex Context

**Gathered:** 2026-08-19
**Spec:** `.specs/features/symbol-index/spec.md`
**Status:** Ready for design

---

## Feature Boundary

Build `ISymbolIndex`/`SymbolIndexBuilder`: a global, queryable, in-memory index of every symbol the generator
discovers (syntax-only or semantically resolved), so a future consumer can answer identity/name/candidate
questions without a full-solution scan. This feature does not build that consumer, does not persist a new
artifact, and does not touch `RelationCollector`'s unresolved-target behavior.

---

## Implementation Decisions

### Data source for `IndexedSymbol`

- `SymbolFact` (the existing persisted fact type) is extended with `Name`, `FullyQualifiedName`, `Namespace`,
  `ContainingType`, `ContainingSymbolId`, `Signature`, `Arity`, and normalized `ParameterTypes` — rather than
  building a second, parallel symbol model that duplicates what `SymbolFactEnricher` already computes and
  discards.
- Consequence, not separately asked but mechanically required: `FactualJsonSerializer.SchemaVersion` moves
  `2` → `3`, and every approved JSON/Verify snapshot containing a `SymbolFactJson` gets re-approved as part of
  this feature's tasks — surfaced, not silently absorbed.
- User's choice, recommended option accepted as-is.

### Consumer wiring

- Standalone only. `ISymbolIndex` ships as a directly-testable query component; no `RelationResolver` gets
  built, and `RelationCollector`'s `RelationFact.TargetId`/`UnresolvedReason` behavior is untouched by this
  feature.
- User's choice, recommended option accepted as-is.
- **Important boundary, not separately asked, inferred from AD-015's own documented lesson in this codebase:**
  "standalone" governs whether this feature *resolves relations* — it does not mean the index gets built only
  in isolated unit tests. `SolutionAnalysisIndex`/`DetectorHost` were built and unit-tested but never invoked by
  the real pipeline (confirmed by grep, zero references outside `Detection/*`), and that silent-dead-code fate
  is exactly what this feature's spec Goal #3 and Assumptions-table "actual construction point" entry exist to
  avoid. `SymbolIndexBuilder.Build` is exercised against a real `AnalysisEngine.AnalyzeAsync` run over
  `fixtures/SyntheticSolution`, even though nothing downstream consumes the result yet.

### Identity scheme

- `IndexedSymbol.Id` reuses the existing `SymbolFactId.Value` verbatim (AD-014's grammar,
  `CreateResolved`/`CreateSyntactic`/`CreateFallback`) rather than the parallel identity scheme sketched in the
  user's original section 6-7. This was resolved by reading the code (Knowledge Verification Chain step 1), not
  asked — the existing type already satisfies the user's own section 7 rule exactly.

### Agent's Discretion

- Exact normalization table boundaries (full C# predefined-type list vs. just the 7 named examples), the
  `params`/optional-parameter argument-count limitation, and the `SchemaVersion` bump mechanics are left to
  Design/Tasks — logged as defaults in spec.md's Assumptions table, open to override before Design starts.

### Declined / Undiscussed Gray Areas → Assumptions

All gray areas surfaced during this Specify pass were resolved either by the two explicit questions above or by
reading the existing codebase (identity scheme, normalization scope, `params` limitation, schema-version
mechanics) — see `spec.md`'s Assumptions & Open Questions table for the full list with rationale. None were
declined; none went undiscussed.

---

## Specific References

- `fixtures/SyntheticSolution/Acme.Payments/PaymentsService.cs` (`PaymentsService.AuthorizePayment`) is reused
  as the concrete P1/P2 Independent Test target — it's the same fixture `relation-collector` already proved
  against, and its shape happens to match the user's own `PaymentsClient`/`AuthorizePayment` examples in section
  2 and section 8 of the original request closely enough to use directly instead of inventing new fixture code.
- The two-namespace `PaymentService` ambiguity example in the user's own section 13
  (`Company.Legacy.PaymentService` / `Company.Payments.PaymentService`) is used verbatim as the P2 ambiguity
  Independent Test — no equivalent collision exists in `fixtures/SyntheticSolution` today, so this one stays a
  hand-built unit fixture.

## Deferred Ideas

- `RelationResolver` itself (the component that would actually consume `ISymbolIndex.FindCandidates` to bind
  `RelationFact.TargetId`) — explicitly named by the user's own spec as the next stage, not this feature.
- `facts/symbol-index.json` / `facts/symbols.jsonl` persistence — the user's own section 16 already marks this
  optional for v1; deferred rather than bundled with the `SymbolFact` schema extension already in scope.
- Standalone `parameter` `IndexedSymbol` entities (section 5's full kind list) — parameter *types* are indexed
  as part of a method's `ParameterTypes`; a parameter as its own addressable symbol is future expansion per the
  user's own "pode ser expandido posteriormente."
- User-defined `using` alias resolution (`using Money = System.Decimal;`) for the normalization rule — a
  materially different, per-document problem from the built-in alias table the user actually listed.
