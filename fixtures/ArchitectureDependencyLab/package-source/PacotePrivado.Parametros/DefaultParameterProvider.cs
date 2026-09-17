namespace PacotePrivado.Parametros;

// SCENARIO:PKG-001
public class DefaultParameterProvider : IParameterProvider
{
    private readonly Dictionary<string, ParameterValue> _params = new()
    {
        ["TaxaJurosPadrao"] = new ParameterValue("TaxaJurosPadrao", "0.05", "decimal"),
        ["LimiteMaximoSimulacao"] = new ParameterValue("LimiteMaximoSimulacao", "1000000", "int"),
        ["AmbienteAtivo"] = new ParameterValue("AmbienteAtivo", "Sintetico", "string")
    };

    public Task<ParameterValue?> GetParameterAsync(string name, CancellationToken cancellationToken = default)
    {
        _params.TryGetValue(name, out var val);
        return Task.FromResult(val);
    }
}
