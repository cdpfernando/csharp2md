namespace Csharp2Md.Analysis.Storage;

public sealed class PublicationRejectedException : Exception
{
    public string Gate { get; }

    public string Detail { get; }

    public PublicationRejectedException(string gate, string detail)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(detail);

        Gate = gate;
        Detail = detail;
    }
}
