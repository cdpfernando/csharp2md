using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SistemaB.Infraestrutura;

#nullable disable

namespace SistemaB.Infraestrutura.Migrations;

// SCENARIO:DATA-007
[DbContext(typeof(ContratacaoDbContext))]
partial class ContratacaoDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("SistemaB.Dominio.Contrato", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT")
                .HasColumnName("id");

            b.Property<string>("Numero")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("numero");

            b.Property<decimal>("ValorTotal")
                .HasColumnType("TEXT")
                .HasColumnName("valor_total");

            b.HasKey("Id");

            b.ToTable("contratos", (string)null);
        });

        modelBuilder.Entity("SistemaB.Dominio.ItemContrato", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT")
                .HasColumnName("id");

            b.Property<Guid>("ContratoId")
                .HasColumnType("TEXT")
                .HasColumnName("contrato_id");

            b.Property<string>("Descricao")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("descricao");

            b.Property<decimal>("Valor")
                .HasColumnType("TEXT")
                .HasColumnName("valor");

            b.HasKey("Id");

            b.HasIndex("ContratoId");

            b.ToTable("itens_contrato", (string)null);
        });

        modelBuilder.Entity("SistemaB.Dominio.ItemContrato", b =>
        {
            b.HasOne("SistemaB.Dominio.Contrato", null)
                .WithMany("Itens")
                .HasForeignKey("ContratoId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
