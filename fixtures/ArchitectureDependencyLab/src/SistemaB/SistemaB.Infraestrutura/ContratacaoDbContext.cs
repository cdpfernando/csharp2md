using Microsoft.EntityFrameworkCore;
using SistemaB.Dominio;

namespace SistemaB.Infraestrutura;

// SCENARIO:DATA-001
public class ContratacaoDbContext : DbContext
{
    public ContratacaoDbContext(DbContextOptions<ContratacaoDbContext> options) : base(options)
    {
    }

    // SCENARIO:DATA-001
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<ItemContrato> ItensContrato => Set<ItemContrato>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SCENARIO:DATA-002
        modelBuilder.Entity<Contrato>(entity =>
        {
            entity.ToTable("contratos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Numero).HasColumnName("numero").IsRequired();
            entity.Property(e => e.ValorTotal).HasColumnName("valor_total");

            entity.HasMany(e => e.Itens)
                .WithOne()
                .HasForeignKey(i => i.ContratoId);
        });

        // SCENARIO:DATA-002
        modelBuilder.Entity<ItemContrato>(entity =>
        {
            entity.ToTable("itens_contrato");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ContratoId).HasColumnName("contrato_id");
            entity.Property(e => e.Descricao).HasColumnName("descricao").IsRequired();
            entity.Property(e => e.Valor).HasColumnName("valor");
        });
    }
}
