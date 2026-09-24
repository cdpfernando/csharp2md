using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PacotePrivado.Parametros;

namespace SistemaC.ApiMonolitica;

public record ValidarTokenRequest(string Token);
public record ValidarTokenResponse(bool Valido, string Usuario, string Perfil);

// SCENARIO:HTTP-C-001
public class ServicoIdentidadeClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ServicoIdentidadeClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ValidarTokenResponse> ValidarTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        // SCENARIO:HTTP-C-001
        var client = _httpClientFactory.CreateClient("ServicoIdentidade");
        var response = await client.PostAsJsonAsync("/v1/tokens/validar", new ValidarTokenRequest(token), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ValidarTokenResponse(false, string.Empty, string.Empty);
        }

        return await response.Content.ReadFromJsonAsync<ValidarTokenResponse>(cancellationToken: cancellationToken)
            ?? new ValidarTokenResponse(false, string.Empty, string.Empty);
    }
}

// SCENARIO:CACHE-C-001
// SCENARIO:CACHE-002
public class ServicoDeCache
{
    private readonly IDistributedCache _cache;
    private readonly IParameterProvider _parameterProvider;

    public ServicoDeCache(IDistributedCache cache, IParameterProvider parameterProvider)
    {
        _cache = cache;
        _parameterProvider = parameterProvider;
    }

    public async Task<string?> ObterParametroComCacheAsync(string chave, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"param:{chave}";
        var valorEmCache = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(valorEmCache))
        {
            return valorEmCache;
        }

        // SCENARIO:PKG-001
        var param = await _parameterProvider.GetParameterAsync(chave, cancellationToken);
        if (param != null)
        {
            await _cache.SetStringAsync(cacheKey, param.Value, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            }, cancellationToken);
            return param.Value;
        }

        return null;
    }
}

// SCENARIO:CACHE-D-001
public class ServicoAutorizacaoComFallback
{
    private readonly IDistributedCache _cache;

    public ServicoAutorizacaoComFallback(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> VerificarAutorizacaoAsync(string usuario, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _cache.GetStringAsync($"auth:perm:{usuario}", cancellationToken);
            if (status != null)
            {
                return status == "PERMITIDO";
            }
        }
        catch (Exception)
        {
            // SCENARIO:CACHE-D-001
            // Fallback permissivo de autorização quando o cache falhar (anti-pattern deliberado)
            return true;
        }

        return true;
    }
}
