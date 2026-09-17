using Microsoft.EntityFrameworkCore;
using SistemaE.Nucleo;

namespace SistemaE.Infraestrutura;

// SCENARIO:DATA-001
public class SistemaEDbContext : DbContext
{
    public SistemaEDbContext(DbContextOptions<SistemaEDbContext> options) : base(options)
    {
    }

    // SCENARIO:DATA-001
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<ItemVenda> ItensVenda => Set<ItemVenda>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SCENARIO:DATA-002
        modelBuilder.Entity<Venda>(entity =>
        {
            entity.ToTable("vendas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Codigo).HasColumnName("codigo").IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").IsRequired();
            entity.Property(e => e.ValorTotal).HasColumnName("valor_total");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.DataVenda).HasColumnName("data_venda");

            entity.HasMany(e => e.Itens)
                .WithOne()
                .HasForeignKey(i => i.VendaId);
        });

        // SCENARIO:DATA-002
        modelBuilder.Entity<ItemVenda>(entity =>
        {
            entity.ToTable("itens_venda");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VendaId).HasColumnName("venda_id");
            entity.Property(e => e.ProdutoId).HasColumnName("produto_id").IsRequired();
            entity.Property(e => e.PrecoUnitario).HasColumnName("preco_unitario");
            entity.Property(e => e.Quantidade).HasColumnName("quantidade");
        });
    }
}
