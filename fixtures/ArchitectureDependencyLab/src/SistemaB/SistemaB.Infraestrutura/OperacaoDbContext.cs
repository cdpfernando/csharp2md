using Microsoft.EntityFrameworkCore;
using SistemaB.Dominio;

namespace SistemaB.Infraestrutura;

// SCENARIO:DATA-001
public class OperacaoDbContext : DbContext
{
    public OperacaoDbContext(DbContextOptions<OperacaoDbContext> options) : base(options)
    {
    }

    // SCENARIO:DATA-001
    public DbSet<Operacao> Operacoes => Set<Operacao>();
    public DbSet<HistoricoOperacao> HistoricoOperacoes => Set<HistoricoOperacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SCENARIO:DATA-002
        modelBuilder.Entity<Operacao>(entity =>
        {
            entity.ToTable("operacoes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Descricao).HasColumnName("descricao").IsRequired();
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
        });

        // SCENARIO:DATA-002
        modelBuilder.Entity<HistoricoOperacao>(entity =>
        {
            entity.ToTable("historico_operacao");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OperacaoId).HasColumnName("operacao_id");
            entity.Property(e => e.Descricao).HasColumnName("descricao").IsRequired();
            entity.Property(e => e.DataRegistro).HasColumnName("data_registro");
        });
    }
}
