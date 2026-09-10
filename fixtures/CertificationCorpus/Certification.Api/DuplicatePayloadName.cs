// GCPC-090: the second half of the same-named-payload-type pair for the contract regression added in
// Certification.Messaging/ContractShapes.cs (T4). Certification.Api carries no reference to
// Certification.Messaging (and vice versa), so this `Receipt` and
// `Certification.Messaging.Receipt` are genuinely unrelated types that happen to share a name — the
// exact shape GCPC-090 forbids merging into one contract by name, structural similarity, path or
// prefix.

namespace Certification.Api;

public sealed record Receipt(Guid OrderId, string Reference);
