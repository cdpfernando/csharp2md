using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SistemaE.Copia.Nucleo;

namespace SistemaE.Copia.Infraestrutura;

// SCENARIO:DATA-001
public class VendaRepository
{
    private readonly SistemaEDbContext _context;

    public VendaRepository(SistemaEDbContext context)
    {
        _context = context;
    }

    // SCENARIO:DATA-004
    public async Task SalvarAsync(Venda venda, CancellationToken cancellationToken = default)
    {
        _context.Vendas.Add(venda);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // SCENARIO:DATA-004
    public async Task<Venda?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Vendas
            .Include(v => v.Itens)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }
}

// SCENARIO:CALL-E-002
// Implementacao concreta de infraestrutura compartilhada usada diretamente por cada modulo
public class SharedInfraAuditService
{
    public Task RegistrarPassoModuloAsync(string nomeModulo, string operacao, string identificador, CancellationToken cancellationToken = default)
    {
        // Infraestrutura compartilhada concreta chamada diretamente
        return Task.CompletedTask;
    }
}

// SCENARIO:HTTP-E-001
public class ServicoCatalogoClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ServicoCatalogoClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<decimal> ObterPrecoItemAsync(string itemId, CancellationToken cancellationToken = default)
    {
        // SCENARIO:HTTP-E-001
        var client = _httpClientFactory.CreateClient("ServicoCatalogo");
        var response = await client.GetAsync($"/v1/itens/{itemId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return 100m;
        }

        return 100m; // Fallback padrao sintético
    }
}

// SCENARIO:HTTP-E-002
public class ServicoFormalizacaoClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ServicoFormalizacaoClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> EnviarPropostaAsync(PropostaVenda proposta, CancellationToken cancellationToken = default)
    {
        // SCENARIO:HTTP-E-002
        var client = _httpClientFactory.CreateClient("ServicoFormalizacao");
        var response = await client.PostAsJsonAsync("/v1/propostas", proposta, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
