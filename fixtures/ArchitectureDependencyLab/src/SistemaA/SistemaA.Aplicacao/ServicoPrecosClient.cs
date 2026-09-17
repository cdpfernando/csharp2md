using System.Net.Http.Json;

namespace SistemaA.Aplicacao;

public interface IServicoPrecosClient
{
    Task<PrecoProdutoDto?> ObterPrecoAsync(string produtoId, CancellationToken cancellationToken = default);
}

// SCENARIO:HTTP-A-001
public class ServicoPrecosClient : IServicoPrecosClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ServicoPrecosClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<PrecoProdutoDto?> ObterPrecoAsync(string produtoId, CancellationToken cancellationToken = default)
    {
        // SCENARIO:HTTP-A-001
        var client = _httpClientFactory.CreateClient("ServicoPrecos");
        var response = await client.GetAsync($"/v1/precos/{produtoId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new PrecoProdutoDto(produtoId, 100.00m); // Fallback padrão sintético
        }

        return await response.Content.ReadFromJsonAsync<PrecoProdutoDto>(cancellationToken: cancellationToken);
    }
}
