namespace Publication.Lib;

public sealed class NestedSignatures
{
    public void NamedTuple((string Name, int Age) person)
    {
    }

    public void NestedGeneric(Dictionary<(string, int), int> map)
    {
    }

    public void MultidimensionalArray(int[,,] grid)
    {
    }

    public void Mixed(
        List<(string Name, int Age)> items,
        int[,,] grid,
        (bool Ok, byte[] Buffer) packet)
    {
    }
}
