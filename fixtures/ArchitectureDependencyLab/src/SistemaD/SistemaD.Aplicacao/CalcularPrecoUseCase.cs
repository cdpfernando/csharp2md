using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PacotePrivado.Parametros;
using SistemaD.Infraestrutura;
using SistemaD.Nucleo;

namespace SistemaD.Aplicacao;

public record CalcularPrecoRequest(string Codigo, string Categoria, decimal ValorBase);
public record CalculoResultadoDto(Guid Id, string Codigo, decimal ValorFinal);

// SCENARIO:HTTP-D-001
public class ServicoCalculoClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ServicoCalculoClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<decimal> SolicitarCalculoAsync(string codigo, decimal valorBase, CancellationToken cancellationToken = default)
    {
        // SCENARIO:HTTP-D-001
        var client = _httpClientFactory.CreateClient("ServicoCalculo");
        var response = await client.PostAsJsonAsync("/v1/calculos", new { Codigo = codigo, Valor = valorBase }, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return valorBase * 1.10m; // Fallback padrao
        }

        var res = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (res.TryGetProperty("valorFinal", out var val) && val.TryGetDecimal(out var dec))
        {
            return dec;
        }

        return valorBase * 1.10m;
    }
}

// SCENARIO:CALL-D-001
public class CalcularPrecoUseCase
{
    private readonly PrecoRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly ServicoCalculoClient _calculoClient;
    private readonly LoteDePrecoChannel _channel;
    private readonly IParameterProvider _parameterProvider;

    public CalcularPrecoUseCase(
        PrecoRepository repository,
        IDistributedCache cache,
        ServicoCalculoClient calculoClient,
        LoteDePrecoChannel channel,
        IParameterProvider parameterProvider)
    {
        _repository = repository;
        _cache = cache;
        _calculoClient = calculoClient;
        _channel = channel;
        _parameterProvider = parameterProvider;
    }

    // SCENARIO:CALL-D-001
    public async Task<CalculoResultadoDto> ExecutarAsync(CalcularPrecoRequest request, CancellationToken cancellationToken = default)
    {
        // SCENARIO:PKG-001
        var param = await _parameterProvider.GetParameterAsync("TaxaJurosPadrao", cancellationToken);
        var taxa = param != null && decimal.TryParse(param.Value, out var t) ? t : 0.05m;

        // SCENARIO:HTTP-D-001
        var valorCalculado = await _calculoClient.SolicitarCalculoAsync(request.Codigo, request.ValorBase, cancellationToken);
        var valorFinal = valorCalculado * (1 + taxa);

        var registro = new PrecoRegistro
        {
            Id = Guid.NewGuid(),
            Codigo = request.Codigo,
            Categoria = request.Categoria,
            Valor = valorFinal,
            AtualizadoEm = DateTime.UtcNow
        };

        // SCENARIO:CALL-D-001
        // 1. Grava no banco
        await _repository.SalvarAsync(registro, cancellationToken);

        // SCENARIO:CACHE-003
        // 2. Atualiza cache logo apos gravacao no banco, sem transacao distribuida (anti-pattern deliberado)
        await _cache.SetStringAsync($"preco:{registro.Codigo}", registro.Valor.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        }, cancellationToken);

        // SCENARIO:MSG-D-001
        // 3. Envia para o canal em memoria para processamento em background
        var item = new PrecoItem(registro.Id, registro.Codigo, registro.Valor);
        var lote = new LoteDePreco(Guid.NewGuid(), registro.Categoria, request.ValorBase, new List<PrecoItem> { item }, DateTime.UtcNow);
        await _channel.EscreverAsync(lote, cancellationToken);

        return new CalculoResultadoDto(registro.Id, registro.Codigo, registro.Valor);
    }
}
