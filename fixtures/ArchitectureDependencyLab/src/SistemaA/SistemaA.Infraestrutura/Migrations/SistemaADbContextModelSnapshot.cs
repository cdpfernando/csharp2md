using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SistemaA.Infraestrutura;

#nullable disable

namespace SistemaA.Infraestrutura.Migrations;

// SCENARIO:DATA-007
[DbContext(typeof(SistemaADbContext))]
partial class SistemaADbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("SistemaA.Nucleo.Cotacao", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT")
                .HasColumnName("id");

            b.Property<DateTime>("DataCriacao")
                .HasColumnType("TEXT")
                .HasColumnName("data_criacao");

            b.Property<string>("ProdutoId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("produto_id");

            b.Property<decimal>("Taxa")
                .HasColumnType("TEXT")
                .HasColumnName("taxa");

            b.Property<decimal>("ValorCalculado")
                .HasColumnType("TEXT")
                .HasColumnName("valor_calculado");

            b.HasKey("Id");

            b.ToTable("cotacoes", (string)null);
        });
    }
}
