namespace Csharp2Md.Core.Topic;

/// <summary>
/// The eleven-value <c>file_type</c> classification (Frontmatter Schema, `file_type` Derivation).
/// Declaration order mirrors the spec's rule table for readability only — the enum carries no
/// ordering semantics of its own; <c>FileTypeClassifier</c> is what enforces first-match-wins.
/// </summary>
public enum FileType
{
    Configuration,
    Controller,
    Handler,
    Service,
    DataAccess,
    Enum,
    Interface,
    Filter,
    Extension,
    Class,
    Index,
}

/// <summary>Distinguishes a source-derived document from a generated index (WIKI-06).</summary>
public enum SourceKind
{
    CodebaseFile,
    CodebaseIndex,
}

/// <summary>
/// The closed model of a frontmatter block (WIKI-06). <see cref="Language"/>, <see cref="CreatedBy"/>,
/// <see cref="SourceService"/>, and <see cref="AnalysisStatus"/> are computed rather than constructor
/// parameters: the spec pins all four to Phase 1 constants, and a settable field would invite a
/// caller to violate that silently. Phase 2 converts <see cref="SourceService"/> and
/// <see cref="AnalysisStatus"/> to real parameters — a change the compiler will point at every
/// construction site.
/// </summary>
public sealed record Frontmatter(
    string Title,
    SourceKind SourceKind,
    string SourcePath,
    string Domain,
    string Topic,
    FileType FileType,
    IReadOnlyList<string> Tags)
{
    public string Language => "csharp";

    public string CreatedBy => "csharp2md";

    /// <summary>Always <c>null</c> in Phase 1; Phase 2 resolves service ownership.</summary>
    public string? SourceService => null;

    /// <summary>Always <c>"pending"</c> in Phase 1.</summary>
    public string AnalysisStatus => "pending";
}
