namespace Acme.Shared.Contracts;

public sealed class CycleProbe
{
    public int Start() => Continue();

    private int Continue() => Start();
}
