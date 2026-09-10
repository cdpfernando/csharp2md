namespace Csharp2Md.Storage.Wire;

/// <summary>
/// A compact, human-legible label for one identity's proven component, type, method, protocol, verb or
/// route value (GCPC-093), cited by the artifact key and ordinal the value came from (GCPC-094) so a
/// wrong label is caught the same way any other repeated value is (GCPC-097). The canonical identity
/// stays the authority; a label is never itself authoritative (GCPC-095).
/// </summary>
public sealed record LabelDto(string Kind, string Value, string ArtifactKey, int Ordinal);
