namespace Csharp2Md.Domain.Facets;

public enum BoundaryProtocol
{
    Http,
    Grpc,
    Messaging,
    Cli,
    Scheduler,
    Function,
}

public enum BoundaryDirection
{
    Inbound,
    Outbound,
}

public enum BoundaryRole
{
    Command,
    Query,
    Event,
    Stream,
    Lifecycle,
}
