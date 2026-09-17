using Microsoft.EntityFrameworkCore;
using SistemaB.Dominio;

namespace SistemaB.Infraestrutura;

// SCENARIO:DATA-001
public class OperacaoRepository
{
    private readonly OperacaoDbContext _context;

    public OperacaoRepository(OperacaoDbContext context)
    {
        _context = context;
    }

    public async Task SalvarOperacaoAsync(Operacao operacao, CancellationToken cancellationToken = default)
    {
        _context.Operacoes.Add(operacao);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // SCENARIO:DATA-005
    public async Task RegistrarHistoricoLiteralParametrizadoAsync(Guid id, Guid operacaoId, string descricao, CancellationToken cancellationToken = default)
    {
        // SQL literal parametrizado para tabela historico_operacao
        var sql = "INSERT INTO historico_operacao (id, operacao_id, descricao, data_registro) VALUES ({0}, {1}, {2}, {3})";
        await _context.Database.ExecuteSqlRawAsync(sql, new object[] { id.ToString(), operacaoId.ToString(), descricao, DateTime.UtcNow }, cancellationToken);
    }

    public async Task<List<HistoricoOperacao>> ObterHistoricoAsync(Guid operacaoId, CancellationToken cancellationToken = default)
    {
        return await _context.HistoricoOperacoes
            .AsNoTracking()
            .Where(h => h.OperacaoId == operacaoId)
            .ToListAsync(cancellationToken);
    }
}
