using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaB.Infraestrutura.Migrations;

// SCENARIO:DATA-007
public partial class InitialContratacao : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "contratos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                numero = table.Column<string>(type: "TEXT", nullable: false),
                valor_total = table.Column<decimal>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_contratos", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "itens_contrato",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                contrato_id = table.Column<Guid>(type: "TEXT", nullable: false),
                descricao = table.Column<string>(type: "TEXT", nullable: false),
                valor = table.Column<decimal>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_itens_contrato", x => x.id);
                table.ForeignKey(
                    name: "FK_itens_contrato_contratos_contrato_id",
                    column: x => x.contrato_id,
                    principalTable: "contratos",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_itens_contrato_contrato_id",
            table: "itens_contrato",
            column: "contrato_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "itens_contrato");

        migrationBuilder.DropTable(
            name: "contratos");
    }
}
