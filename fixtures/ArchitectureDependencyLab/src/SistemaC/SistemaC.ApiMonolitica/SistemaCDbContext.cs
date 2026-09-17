using Microsoft.EntityFrameworkCore;

namespace SistemaC.ApiMonolitica;

public class ParametroSistema
{
    public Guid Id { get; set; }
    public string Chave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public DateTime AtualizadoEm { get; set; }
}

// SCENARIO:DATA-001
public class SistemaCDbContext : DbContext
{
    public SistemaCDbContext(DbContextOptions<SistemaCDbContext> options) : base(options)
    {
    }

    // SCENARIO:DATA-001
    public DbSet<ParametroSistema> Parametros => Set<ParametroSistema>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SCENARIO:DATA-002
        modelBuilder.Entity<ParametroSistema>(entity =>
        {
            entity.ToTable("parametros_sistema");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Chave).HasColumnName("chave").IsRequired();
            entity.Property(e => e.Valor).HasColumnName("valor").IsRequired();
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
        });
    }

    // SCENARIO:DATA-005
    // SCENARIO:NEG-009
    public async Task<int> ExecutarStoredProcedureParametrizadaAsync(string chave, CancellationToken cancellationToken = default)
    {
        // Stored procedure chamada por SQL literal parametrizado (sem concatenacao dinamica insegura)
        return await Database.ExecuteSqlRawAsync("EXEC sp_obter_parametro {0}", new object[] { chave }, cancellationToken);
    }
}
