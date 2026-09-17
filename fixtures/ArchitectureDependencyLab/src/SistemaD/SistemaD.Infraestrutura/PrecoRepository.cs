using Microsoft.EntityFrameworkCore;

namespace SistemaD.Infraestrutura;

// SCENARIO:DATA-001
public class PrecoRepository
{
    private readonly SistemaDDbContext _context;

    public PrecoRepository(SistemaDDbContext context)
    {
        _context = context;
    }

    public async Task SalvarAsync(PrecoRegistro registro, CancellationToken cancellationToken = default)
    {
        _context.Precos.Add(registro);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PrecoRegistro?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await _context.Precos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Codigo == codigo, cancellationToken);
    }
}
