using System.Text.Json;

namespace Csharp2Md.Storage;

public sealed record QuarantineRecord(
    string RecordKind,
    string IdentityOrKey,
    string Gate,
    string Detail,
    JsonElement Payload);
