# 04 — Generate shard-aware disposition posting guidance

**What to build:** Keep `retrieval.md` executable when disposition posting families are sharded. The guide must derive its instructions from the posting artifacts produced for the current publication, name an exact artifact only when it exists, and otherwise direct the reader to the matching shard without presenting a nonexistent base key as a file.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] When `postings/unknowns.json` exists, the disposition section preserves the current exact-key navigation.
- [ ] When the unknown posting family is sharded and the base key is absent, the guide describes selection of a matching unknown shard and does not quote the absent base key as an artifact.
- [ ] Frontier postings follow the same exact-versus-sharded behavior.
- [ ] Candidate, unresolved, and open-frontier relation-family instructions retain their existing shard-aware behavior.
- [ ] A real-layout projection test passes the same small ceiling to layout and projection so the posting family itself, not only the relation family, is forced to shard.
- [ ] Every artifact key enclosed in backticks exists in the publication and the existing absent-key validator passes unchanged.
- [ ] `retrieval.md` and all generated artifacts remain within the declared ceiling rules; no new ceiling exclusion is added.
- [ ] Missing posting families are still reported as absent and are not confused with sharded families.
- [ ] Projection output remains deterministic for identical input and ceiling values.
- [ ] Validator strictness, taxonomy, schemas, and retrieval semantics remain unchanged.
