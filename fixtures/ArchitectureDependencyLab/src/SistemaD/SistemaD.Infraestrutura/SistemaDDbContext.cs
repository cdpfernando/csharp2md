using Microsoft.EntityFrameworkCore;

namespace SistemaD.Infraestrutura;

public class PrecoRegistro
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime AtualizadoEm { get; set; }
}

// SCENARIO:DATA-001
public class SistemaDDbContext : DbContext
{
    public SistemaDDbContext(DbContextOptions<SistemaDDbContext> options) : base(options)
    {
    }

    // SCENARIO:DATA-001
    public DbSet<PrecoRegistro> Precos => Set<PrecoRegistro>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SCENARIO:DATA-002
        modelBuilder.Entity<PrecoRegistro>(entity =>
        {
            entity.ToTable("precos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Codigo).HasColumnName("codigo").IsRequired();
            entity.Property(e => e.Categoria).HasColumnName("categoria").IsRequired();
            entity.Property(e => e.Valor).HasColumnName("valor");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
        });
    }
}
