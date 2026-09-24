using System.Net.Http.Json;
using SistemaB.Transversal;

namespace SistemaB.Infraestrutura;

public record ItemCatalogoDto(string Id, string Nome, decimal Preco);

// SCENARIO:HTTP-B-001
public class ServicoCatalogoClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ServicoCatalogoClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ItemCatalogoDto?> ObterItemAsync(string id, CancellationToken cancellationToken = default)
    {
        // SCENARIO:HTTP-B-001
        var client = _httpClientFactory.CreateClient("ServicoCatalogo");
        var response = await client.GetAsync($"/v1/itens/{id}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ItemCatalogoDto(id, "Item Padrao Sintetico", 50.00m);
        }

        return await response.Content.ReadFromJsonAsync<ItemCatalogoDto>(cancellationToken: cancellationToken);
    }
}

// SCENARIO:HTTP-B-002
public class ServicoRiscoClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private static int _failureCount = 0;
    private static DateTime _circuitOpenUntil = DateTime.MinValue;

    public ServicoRiscoClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AvaliacaoRiscoDto> AvaliarRiscoAsync(decimal valorTotal, CancellationToken cancellationToken = default)
    {
        // Circuit breaker simples deliberado
        if (DateTime.UtcNow < _circuitOpenUntil)
        {
            return new AvaliacaoRiscoDto("CB-FALLBACK", "MEDIO", true);
        }

        try
        {
            // SCENARIO:HTTP-B-002
            var client = _httpClientFactory.CreateClient("ServicoRisco");
            var response = await client.PostAsJsonAsync("/v1/avaliacoes", new { Valor = valorTotal }, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _failureCount = 0;
                var res = await response.Content.ReadFromJsonAsync<AvaliacaoRiscoDto>(cancellationToken: cancellationToken);
                return res ?? new AvaliacaoRiscoDto("RISCO-001", "BAIXO", true);
            }
            _failureCount++;
        }
        catch
        {
            _failureCount++;
            if (_failureCount >= 3)
            {
                _circuitOpenUntil = DateTime.UtcNow.AddSeconds(30);
            }
        }

        return new AvaliacaoRiscoDto("RISCO-DEFAULT", "BAIXO", true);
    }
}

public class ContratacaoGatewayAdapter : IContratacaoGateway
{
    private readonly ServicoCatalogoClient _catalogoClient;
    private readonly ServicoRiscoClient _riscoClient;
    private readonly OperacaoRepository _operacaoRepository;

    public ContratacaoGatewayAdapter(
        ServicoCatalogoClient catalogoClient,
        ServicoRiscoClient riscoClient,
        OperacaoRepository operacaoRepository)
    {
        _catalogoClient = catalogoClient;
        _riscoClient = riscoClient;
        _operacaoRepository = operacaoRepository;
    }

    public async Task<bool> VerificarPreliminarAsync(string numeroContrato, CancellationToken cancellationToken = default)
    {
        var item = await _catalogoClient.ObterItemAsync("item-padrao", cancellationToken);
        return item != null;
    }

    public async Task<AvaliacaoRiscoDto> ValidarAsync(decimal valorTotal, CancellationToken cancellationToken = default)
    {
        return await _riscoClient.AvaliarRiscoAsync(valorTotal, cancellationToken);
    }

    public async Task AuditarAsync(Guid contratoId, string status, CancellationToken cancellationToken = default)
    {
        await _operacaoRepository.RegistrarHistoricoLiteralParametrizadoAsync(
            Guid.NewGuid(),
            contratoId,
            $"Auditoria Contrato: {status}",
            cancellationToken);
    }
}
