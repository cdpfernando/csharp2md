# 03 — Round-trip nested canonical symbol signatures

**What to build:** Make canonical symbol signatures containing nested C# type shapes survive storage re-hydration exactly. Parameter and type-argument lists must split only on top-level commas while respecting balanced generic brackets, tuple parentheses, and array-rank brackets, without relaxing structural-corruption validation.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] Named tuple parameters round-trip from Domain to wire and back with exact canonical signature equality.
- [ ] Multiple tuple parameters retain their internal commas and remain separate top-level parameters.
- [ ] Tuples nested inside generic types and generic types nested inside tuples round-trip exactly.
- [ ] Multidimensional array ranks retain their commas and are not interpreted as separate parameters.
- [ ] Mixed nested generic, tuple, and array shapes are handled deterministically.
- [ ] Existing simple and generic signatures retain their current canonical identities and serialized values.
- [ ] Malformed or unbalanced wire signatures remain rejected as structural corruption rather than being silently accepted.
- [ ] A minimized constructor regression containing the diagnosed tuple shape reaches publication without structural corruption.
- [ ] Tests assert full domain equality and exact canonical identity equality, not only successful parsing.
- [ ] The identity namespace, signature fields, escaping rules, taxonomy version, and schema version remain unchanged.
