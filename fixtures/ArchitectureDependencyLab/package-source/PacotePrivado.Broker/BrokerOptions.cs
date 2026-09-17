namespace PacotePrivado.Broker;

// SCENARIO:PKG-002
// SCENARIO:CFG-001
public class BrokerOptions
{
    public string Brokers { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string SchemaRegistryUrl { get; set; } = string.Empty;
    public string ClientPassword { get; set; } = string.Empty;
}
