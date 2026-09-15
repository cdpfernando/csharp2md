namespace Csharp2Md.Core.Analysis;

internal sealed record FactualGraph
{
    public SolutionIdentity Solution { get; }

    public ImmutableArray<LogicalEntity> Entities { get; }

    public ImmutableArray<VariantOccurrence> Occurrences { get; }

    public ImmutableArray<EvidenceRecord> Evidence { get; }

    public ImmutableArray<FactualRelation> Relations { get; }

    public ImmutableArray<KnowledgeGap> Gaps { get; }

    public ImmutableArray<SourceDocumentSnapshot> Sources { get; }

    public ExtractionMeasurements Measurements { get; }

    public FactualGraph(
        SolutionIdentity solution,
        ImmutableArray<LogicalEntity> entities,
        ImmutableArray<VariantOccurrence> occurrences,
        ImmutableArray<EvidenceRecord> evidence,
        ImmutableArray<FactualRelation> relations,
        ImmutableArray<KnowledgeGap> gaps,
        ImmutableArray<SourceDocumentSnapshot> sources,
        ExtractionMeasurements measurements)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(measurements);
        Solution = solution;
        Entities = Own(entities);
        Occurrences = Own(occurrences);
        Evidence = Own(evidence);
        Relations = Own(relations);
        Gaps = Own(gaps);
        Sources = Own(sources);
        Measurements = measurements;
    }

    private static ImmutableArray<T> Own<T>(ImmutableArray<T> items) =>
        items.IsDefault ? ImmutableArray<T>.Empty : ImmutableArray.CreateRange(items);
}

internal sealed record LogicalEntity
{
    public EntityKind Kind { get; }

    public string CanonicalKey { get; }

    public string DisplayName { get; }

    public string? QualifiedName { get; }

    public LogicalEntity(EntityKind kind, string canonicalKey, string displayName, string? qualifiedName)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
        CanonicalKey = CanonicalText.Require(canonicalKey, nameof(canonicalKey));
        DisplayName = CanonicalText.Require(displayName, nameof(displayName));
        QualifiedName = qualifiedName is null ? null : CanonicalText.Require(qualifiedName, nameof(qualifiedName));
    }
}

internal sealed record VariantOccurrence
{
    public string EntityCanonicalKey { get; }

    public ProjectIdentity Project { get; }

    public AnalysisVariant Variant { get; }

    public LogicalLocator Locator { get; }

    public string ShapeDigest { get; }

    public ImmutableArray<string> EvidenceCanonicalKeys { get; }

    public VariantOccurrence(
        string entityCanonicalKey,
        ProjectIdentity project,
        AnalysisVariant variant,
        LogicalLocator locator,
        string shapeDigest,
        ImmutableArray<string> evidenceCanonicalKeys)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(variant);
        ArgumentNullException.ThrowIfNull(locator);
        EntityCanonicalKey = CanonicalText.Require(entityCanonicalKey, nameof(entityCanonicalKey));
        Project = project;
        Variant = variant;
        Locator = locator;
        ShapeDigest = CanonicalText.Require(shapeDigest, nameof(shapeDigest));
        EvidenceCanonicalKeys = evidenceCanonicalKeys.IsDefault
            ? ImmutableArray<string>.Empty
            : ImmutableArray.CreateRange(evidenceCanonicalKeys);
    }
}

internal sealed record AnalysisVariant
{
    public string TargetFramework { get; }

    public string Configuration { get; }

    public ImmutableArray<string> Symbols { get; }

    public string Environment { get; }

    public AnalysisVariant(
        string targetFramework,
        string configuration,
        ImmutableArray<string> symbols,
        string environment)
    {
        TargetFramework = CanonicalText.Require(targetFramework, nameof(targetFramework));
        Configuration = CanonicalText.Require(configuration, nameof(configuration));
        Symbols = symbols.IsDefault ? ImmutableArray<string>.Empty : ImmutableArray.CreateRange(symbols);
        Environment = CanonicalText.Require(environment, nameof(environment));
    }
}

internal sealed record EvidenceRecord
{
    public string CanonicalKey { get; }

    public string DocumentCanonicalKey { get; }

    public AnalysisVariant Variant { get; }

    public SourceSpan Span { get; }

    public string ContentDigest { get; }

    public EvidenceRecord(
        string canonicalKey,
        string documentCanonicalKey,
        AnalysisVariant variant,
        SourceSpan span,
        string contentDigest)
    {
        ArgumentNullException.ThrowIfNull(variant);
        CanonicalKey = CanonicalText.Require(canonicalKey, nameof(canonicalKey));
        DocumentCanonicalKey = CanonicalText.Require(documentCanonicalKey, nameof(documentCanonicalKey));
        Variant = variant;
        Span = span;
        ContentDigest = CanonicalText.Require(contentDigest, nameof(contentDigest));
    }
}

internal sealed record LogicalLocator
{
    public string RelativePath { get; }

    public SourceSpan Span { get; }

    public ProjectIdentity Project { get; }

    public LogicalLocator(string relativePath, SourceSpan span, ProjectIdentity project)
    {
        ArgumentNullException.ThrowIfNull(project);
        RelativePath = LogicalPath.RequireRelative(relativePath, nameof(relativePath));
        Span = span;
        Project = project;
    }
}

internal readonly record struct SourceSpan
{
    public int StartLine { get; }

    public int StartColumn { get; }

    public int EndLine { get; }

    public int EndColumn { get; }

    public SourceSpan(int startLine, int startColumn, int endLine, int endColumn)
    {
        ValidatePosition(startLine, startColumn, nameof(startLine));
        ValidatePosition(endLine, endColumn, nameof(endLine));
        if ((endLine, endColumn).CompareTo((startLine, startColumn)) < 0)
        {
            throw new ArgumentException("A source span's end must not precede its start.", nameof(endLine));
        }

        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    private static void ValidatePosition(int line, int column, string parameterName)
    {
        if (line <= 0 || column <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Source span coordinates must be one-based.");
        }
    }
}

internal sealed record FactualRelation
{
    public string CanonicalKey { get; }

    public string SourceCanonicalKey { get; }

    public string TargetCanonicalKey { get; }

    public string Category { get; }

    public ImmutableArray<string> EvidenceCanonicalKeys { get; }

    public FactualRelation(
        string canonicalKey,
        string sourceCanonicalKey,
        string targetCanonicalKey,
        string category,
        ImmutableArray<string> evidenceCanonicalKeys)
    {
        CanonicalKey = CanonicalText.Require(canonicalKey, nameof(canonicalKey));
        SourceCanonicalKey = CanonicalText.Require(sourceCanonicalKey, nameof(sourceCanonicalKey));
        TargetCanonicalKey = CanonicalText.Require(targetCanonicalKey, nameof(targetCanonicalKey));
        Category = CanonicalText.Require(category, nameof(category));
        EvidenceCanonicalKeys = evidenceCanonicalKeys.IsDefault
            ? ImmutableArray<string>.Empty
            : ImmutableArray.CreateRange(evidenceCanonicalKeys);
    }
}

internal sealed record KnowledgeGap
{
    public string CanonicalKey { get; }

    public GapKind Kind { get; }

    public string Cause { get; }

    public ImmutableArray<string> AffectedEntityCanonicalKeys { get; }

    public ImmutableArray<string> EvidenceCanonicalKeys { get; }

    public KnowledgeGap(
        string canonicalKey,
        GapKind kind,
        string cause,
        ImmutableArray<string> affectedEntityCanonicalKeys,
        ImmutableArray<string> evidenceCanonicalKeys)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        CanonicalKey = CanonicalText.Require(canonicalKey, nameof(canonicalKey));
        Kind = kind;
        Cause = CanonicalText.Require(cause, nameof(cause));
        AffectedEntityCanonicalKeys = affectedEntityCanonicalKeys.IsDefault
            ? ImmutableArray<string>.Empty
            : ImmutableArray.CreateRange(affectedEntityCanonicalKeys);
        EvidenceCanonicalKeys = evidenceCanonicalKeys.IsDefault
            ? ImmutableArray<string>.Empty
            : ImmutableArray.CreateRange(evidenceCanonicalKeys);
    }
}

internal sealed record SourceDocumentSnapshot
{
    public string CanonicalKey { get; }

    public LogicalLocator Locator { get; }

    public bool IsTest { get; }

    public string ContentDigest { get; }

    public SourceDocumentSnapshot(string canonicalKey, LogicalLocator locator, bool isTest, string contentDigest)
    {
        ArgumentNullException.ThrowIfNull(locator);
        CanonicalKey = CanonicalText.Require(canonicalKey, nameof(canonicalKey));
        Locator = locator;
        IsTest = isTest;
        ContentDigest = CanonicalText.Require(contentDigest, nameof(contentDigest));
    }
}

internal sealed record ExtractionMeasurements
{
    public int ExtractedCount { get; }

    public int FilteredCount { get; }

    public ExtractionMeasurements(int extractedCount, int filteredCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(extractedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(filteredCount);
        ExtractedCount = extractedCount;
        FilteredCount = filteredCount;
    }
}

internal sealed record SolutionIdentity
{
    public string CanonicalKey { get; }

    public string LogicalRelativePath { get; }

    public SolutionIdentity(string canonicalKey, string logicalRelativePath)
    {
        CanonicalKey = CanonicalText.Require(canonicalKey, nameof(canonicalKey));
        LogicalRelativePath = LogicalPath.RequireRelative(logicalRelativePath, nameof(logicalRelativePath));
    }
}

internal sealed record ProjectIdentity
{
    public string CanonicalKey { get; }

    public string LogicalRelativePath { get; }

    public ProjectIdentity(string canonicalKey, string logicalRelativePath)
    {
        CanonicalKey = CanonicalText.Require(canonicalKey, nameof(canonicalKey));
        LogicalRelativePath = LogicalPath.RequireRelative(logicalRelativePath, nameof(logicalRelativePath));
    }
}

internal enum EntityKind
{
    Solution,
    Project,
    Document,
    Symbol,
    Callable,
    Component,
    DeploymentUnit,
    EntryPoint,
    BoundaryOperation,
    ExternalSystem,
    Contract,
    DataStore,
    DataObject,
    DataField,
    DataOperation,
    Configuration,
}

internal enum GapKind
{
    Candidate,
    Unknown,
    OpenFrontier,
}

internal static class LogicalPath
{
    public static string RequireRelative(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);

        if (path[0] is '/' or '\\' ||
            path.Contains('\\', StringComparison.Ordinal) ||
            (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':'))
        {
            throw new ArgumentException("The path must be a logical relative path using forward slashes.", parameterName);
        }

        var segments = path.Split('/');
        if (segments.Any(static segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("The path must not contain empty or dot segments.", parameterName);
        }

        return path;
    }
}

internal static class CanonicalText
{
    public static string Require(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Contains('\r', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal) ||
            value.Contains('\t', StringComparison.Ordinal) ||
            value.Contains("  ", StringComparison.Ordinal))
        {
            throw new ArgumentException("The value must be a canonical single-line value.", parameterName);
        }

        return value;
    }
}
