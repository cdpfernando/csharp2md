using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SistemaE.Infraestrutura;

#nullable disable

namespace SistemaE.Infraestrutura.Migrations;

// SCENARIO:DATA-007
[DbContext(typeof(SistemaEDbContext))]
partial class SistemaEDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("SistemaE.Nucleo.Venda", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT")
                .HasColumnName("id");

            b.Property<string>("ClienteId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("cliente_id");

            b.Property<string>("Codigo")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("codigo");

            b.Property<DateTime>("DataVenda")
                .HasColumnType("TEXT")
                .HasColumnName("data_venda");

            b.Property<string>("Status")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("status");

            b.Property<decimal>("ValorTotal")
                .HasColumnType("TEXT")
                .HasColumnName("valor_total");

            b.HasKey("Id");

            b.ToTable("vendas", (string)null);
        });

        modelBuilder.Entity("SistemaE.Nucleo.ItemVenda", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT")
                .HasColumnName("id");

            b.Property<decimal>("PrecoUnitario")
                .HasColumnType("TEXT")
                .HasColumnName("preco_unitario");

            b.Property<string>("ProdutoId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("produto_id");

            b.Property<int>("Quantidade")
                .HasColumnType("INTEGER")
                .HasColumnName("quantidade");

            b.Property<Guid>("VendaId")
                .HasColumnType("TEXT")
                .HasColumnName("venda_id");

            b.HasKey("Id");

            b.HasIndex("VendaId");

            b.ToTable("itens_venda", (string)null);
        });

        modelBuilder.Entity("SistemaE.Nucleo.ItemVenda", b =>
        {
            b.HasOne("SistemaE.Nucleo.Venda", null)
                .WithMany("Itens")
                .HasForeignKey("VendaId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
