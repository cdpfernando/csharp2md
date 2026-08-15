namespace Csharp2Md.Core.Graph;

/// <summary>The detector family a signal came from.</summary>
public enum DependencyKind
{
    Http,
    Grpc,
    Messaging,
    DirectReference,
}
