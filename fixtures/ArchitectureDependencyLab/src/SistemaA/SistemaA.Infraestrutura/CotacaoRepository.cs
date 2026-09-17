using Microsoft.EntityFrameworkCore;
using SistemaA.Nucleo;

namespace SistemaA.Infraestrutura;

// SCENARIO:DATA-001
public class CotacaoRepository
{
    private readonly SistemaADbContext _context;

    public CotacaoRepository(SistemaADbContext context)
    {
        _context = context;
    }

    // SCENARIO:DATA-004
    public async Task SaveAsync(Cotacao cotacao, CancellationToken cancellationToken = default)
    {
        _context.Cotacoes.Add(cotacao);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // SCENARIO:DATA-004
    public async Task<Cotacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Cotacoes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }
}
