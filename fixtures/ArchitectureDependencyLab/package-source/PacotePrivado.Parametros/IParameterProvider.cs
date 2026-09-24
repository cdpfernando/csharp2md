namespace PacotePrivado.Parametros;

// SCENARIO:PKG-001
public interface IParameterProvider
{
    Task<ParameterValue?> GetParameterAsync(string name, CancellationToken cancellationToken = default);
}
