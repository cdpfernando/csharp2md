using Microsoft.EntityFrameworkCore;

namespace SistemaD.Infraestrutura;

public static class PrecoSqlQueries
{
    // SCENARIO:DATA-005
    public static async Task<List<PrecoRegistro>> ConsultarPorCategoriaLiteralAsync(
        SistemaDDbContext context,
        string categoria,
        CancellationToken cancellationToken = default)
    {
        // Consulta SQL literal parametrizada
        var query = "SELECT id, codigo, categoria, valor, atualizado_em FROM precos WHERE categoria = {0}";
        return await context.Precos
            .FromSqlRaw(query, categoria)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // SCENARIO:DATA-006
    public static async Task<List<PrecoRegistro>> ConsultarPorCategoriaDinamicoAsync(
        SistemaDDbContext context,
        string categoria,
        CancellationToken cancellationToken = default)
    {
        // Consulta SQL construída dinamicamente por concatenação (anti-pattern deliberado)
        var queryDinamica = "SELECT id, codigo, categoria, valor, atualizado_em FROM precos WHERE categoria = '" + categoria + "'";
        return await context.Precos
            .FromSqlRaw(queryDinamica)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
