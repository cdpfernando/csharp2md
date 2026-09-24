using Microsoft.EntityFrameworkCore;
using SistemaB.Dominio;

namespace SistemaB.Infraestrutura;

// SCENARIO:DATA-001
public class ContratacaoRepository
{
    private readonly ContratacaoDbContext _context;

    public ContratacaoRepository(ContratacaoDbContext context)
    {
        _context = context;
    }

    // SCENARIO:DATA-004
    public async Task SalvarContratoAsync(Contrato contrato, CancellationToken cancellationToken = default)
    {
        _context.Contratos.Add(contrato);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // SCENARIO:DATA-004
    public async Task<Contrato?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Contratos
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }
}
