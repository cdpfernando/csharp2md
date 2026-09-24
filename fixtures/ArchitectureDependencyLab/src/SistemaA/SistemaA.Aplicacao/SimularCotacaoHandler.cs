using PacotePrivado.Parametros;
using SistemaA.Infraestrutura;
using SistemaA.Nucleo;

namespace SistemaA.Aplicacao;

// SCENARIO:CALL-A-001
public class SimularCotacaoHandler
{
    private readonly IServicoPrecosClient _precosClient;
    private readonly CotacaoRepository _cotacaoRepository;
    private readonly IParameterProvider _parameterProvider;

    public SimularCotacaoHandler(
        IServicoPrecosClient precosClient,
        CotacaoRepository cotacaoRepository,
        IParameterProvider parameterProvider)
    {
        _precosClient = precosClient;
        _cotacaoRepository = cotacaoRepository;
        _parameterProvider = parameterProvider;
    }

    // SCENARIO:CALL-A-001
    public async Task<CotacaoResultadoDto> HandleAsync(SimularCotacaoCommand command, CancellationToken cancellationToken = default)
    {
        // SCENARIO:PKG-001
        var taxaParam = await _parameterProvider.GetParameterAsync("TaxaJurosPadrao", cancellationToken);
        var taxa = taxaParam != null && decimal.TryParse(taxaParam.Value, out var val) ? val : 0.05m;

        // SCENARIO:HTTP-A-001
        var precoInfo = await _precosClient.ObterPrecoAsync(command.ProdutoId, cancellationToken);
        var precoBase = precoInfo?.PrecoBase ?? 100m;

        var valorFinal = (precoBase + command.ValorDesejado) * (1 + taxa);

        var cotacao = new Cotacao
        {
            Id = Guid.NewGuid(),
            ProdutoId = command.ProdutoId,
            ValorCalculado = valorFinal,
            Taxa = taxa,
            DataCriacao = DateTime.UtcNow
        };

        // SCENARIO:CALL-A-001
        // SCENARIO:DATA-001
        await _cotacaoRepository.SaveAsync(cotacao, cancellationToken);

        return new CotacaoResultadoDto(cotacao.Id, cotacao.ProdutoId, cotacao.ValorCalculado, cotacao.Taxa);
    }
}
