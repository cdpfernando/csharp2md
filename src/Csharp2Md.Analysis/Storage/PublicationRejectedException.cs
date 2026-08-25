namespace Csharp2Md.Analysis.Storage;

public sealed class PublicationRejectedException : Exception
{
    public string Gate { get; }

    public string Detail { get; }

    public PublicationRejectedException(string gate, string detail)
        : this(gate, detail, innerException: null)
    {
    }

    public PublicationRejectedException(string gate, string detail, Exception? innerException)
        : base(FormatMessage(gate, detail), innerException)
    {
        Gate = gate;
        Detail = detail;
    }

    private static string FormatMessage(string gate, string detail)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(detail);
        return $"{gate}: {detail}";
    }
}
