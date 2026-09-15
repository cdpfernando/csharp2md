namespace Publication.Lib;

public interface IBuilder
{
}

public sealed class OrderBuilder : IBuilder
{
    public OrderBuilder()
    {
        Seed();
    }

    public void Seed()
    {
    }
}
