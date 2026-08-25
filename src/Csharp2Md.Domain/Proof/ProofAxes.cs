namespace Csharp2Md.Domain.Proof;

public enum EvidenceMethod
{
    Semantic,
    Syntactic,
    Configured,
}

public enum Resolution
{
    Confirmed,
    Candidate,
    Unresolved,
}

public enum Frontier
{
    Closed,
    Open,
}
