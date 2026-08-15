namespace Csharp2Md.Core.Graph;

/// <summary>
/// The five values P2-12 (as amended) admits — every recorded edge carries exactly one.
/// </summary>
public enum CommunicationType
{
    SincronoBloqueante,
    AssincronoFireAndForget,
    PubSubEvento,
    StreamingBidirecional,

    /// <summary>P2-12 (amended): compile-time coupling, labeled like any other edge.</summary>
    DirectReference,
}
