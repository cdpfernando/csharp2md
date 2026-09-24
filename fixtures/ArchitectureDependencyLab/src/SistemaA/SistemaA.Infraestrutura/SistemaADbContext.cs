using Microsoft.EntityFrameworkCore;
using SistemaA.Nucleo;

namespace SistemaA.Infraestrutura;

// SCENARIO:DATA-001
public class SistemaADbContext : DbContext
{
    public SistemaADbContext(DbContextOptions<SistemaADbContext> options) : base(options)
    {
    }

    // SCENARIO:DATA-001
    public DbSet<Cotacao> Cotacoes => Set<Cotacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SCENARIO:DATA-002
        modelBuilder.Entity<Cotacao>(entity =>
        {
            entity.ToTable("cotacoes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProdutoId).HasColumnName("produto_id").IsRequired();
            entity.Property(e => e.ValorCalculado).HasColumnName("valor_calculado");
            entity.Property(e => e.Taxa).HasColumnName("taxa");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
        });
    }
}
