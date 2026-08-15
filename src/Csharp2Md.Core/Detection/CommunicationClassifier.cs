using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Detection;

/// <summary>
/// The call shapes the design's classification table distinguishes. Each detector reports the
/// shape it observed; the mapping to a communication type lives in one place.
/// </summary>
public enum CallShape
{
    /// <summary>Awaited, or its result otherwise consumed.</summary>
    ResultConsumed,

    /// <summary>Not awaited, result discarded.</summary>
    ResultDiscarded,

    /// <summary>A single request/response exchange.</summary>
    Unary,

    /// <summary>A streaming or duplex exchange.</summary>
    Streaming,

    /// <summary>The kind alone determines the classification.</summary>
    NotApplicable,
}

/// <summary>
/// Static pure function implementing every row of the design's communication-type classification
/// table, so each rule is unit-testable without constructing a detector or a pipeline.
/// </summary>
public static class CommunicationClassifier
{
    public static CommunicationType Classify(DependencyKind kind, CallShape shape) => (kind, shape) switch
    {
        (DependencyKind.Http, CallShape.ResultConsumed) => CommunicationType.SincronoBloqueante,
        (DependencyKind.Http, CallShape.ResultDiscarded) => CommunicationType.AssincronoFireAndForget,
        (DependencyKind.Grpc, CallShape.Unary) => CommunicationType.SincronoBloqueante,
        (DependencyKind.Grpc, CallShape.Streaming) => CommunicationType.StreamingBidirecional,
        (DependencyKind.Messaging, _) => CommunicationType.PubSubEvento,
        (DependencyKind.DirectReference, _) => CommunicationType.DirectReference,

        // No table row covers this pair. Guessing a type here would put a fabricated edge in
        // dependencies.json, so the caller is told it asked something meaningless instead.
        _ => throw new ArgumentOutOfRangeException(
            nameof(shape),
            shape,
            $"No classification rule maps {kind} with call shape {shape}."),
    };
}
