# 02 — Preserve valid evidence for contains relations

**What to build:** Allow valid symbol-level `contains` relations to be constructed when a symbol owns observations but all of its own observations are behavioral. Evidence selection must occur after structural scoping: qualifying symbol-owned evidence wins, qualifying document-scoped evidence is the fallback, and no behavioral or fabricated evidence may enter the confirmed relation.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] Qualifying symbol-owned structural observations remain the preferred evidence for a document-to-symbol `contains` relation.
- [ ] When the symbol owns only `Invocation` or `DataAccess` observations, qualifying structural observations from the containing document are used as the fallback.
- [ ] Every emitted confirmed `contains` relation has a non-empty evidence chain containing no `Invocation` or `DataAccess` observation.
- [ ] When neither scope has qualifying evidence, no confirmed relation with empty or invented evidence is constructed; the run records the specified safe operational diagnostic and explicit degraded outcome instead of throwing.
- [ ] The domain invariant requiring at least one observation for a confirmed relation remains unchanged.
- [ ] A minimized builder-constructor regression reaches the real observation-extraction and publication path and commits successfully.
- [ ] The regression test reads the resulting relation and verifies evidence ownership, evidence kinds, and non-empty cardinality rather than asserting only that execution did not throw.
- [ ] Existing behavior for symbols that already have their own structural evidence remains unchanged.
- [ ] No new fact family, observation kind, relation kind, facet, identity namespace, or schema version is introduced.
