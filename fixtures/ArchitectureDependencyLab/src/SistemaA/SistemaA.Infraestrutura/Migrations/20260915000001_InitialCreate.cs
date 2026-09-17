using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaA.Infraestrutura.Migrations;

// SCENARIO:DATA-007
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "cotacoes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                produto_id = table.Column<string>(type: "TEXT", nullable: false),
                valor_calculado = table.Column<decimal>(type: "TEXT", nullable: false),
                taxa = table.Column<decimal>(type: "TEXT", nullable: false),
                data_criacao = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cotacoes", x => x.id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "cotacoes");
    }
}
