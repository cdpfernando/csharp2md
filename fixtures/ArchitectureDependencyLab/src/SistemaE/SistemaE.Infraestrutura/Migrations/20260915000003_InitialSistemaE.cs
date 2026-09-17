using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaE.Infraestrutura.Migrations;

// SCENARIO:DATA-007
public partial class InitialSistemaE : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "vendas",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                codigo = table.Column<string>(type: "TEXT", nullable: false),
                cliente_id = table.Column<string>(type: "TEXT", nullable: false),
                valor_total = table.Column<decimal>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                data_venda = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_vendas", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "itens_venda",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                venda_id = table.Column<Guid>(type: "TEXT", nullable: false),
                produto_id = table.Column<string>(type: "TEXT", nullable: false),
                preco_unitario = table.Column<decimal>(type: "TEXT", nullable: false),
                quantidade = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_itens_venda", x => x.id);
                table.ForeignKey(
                    name: "FK_itens_venda_vendas_venda_id",
                    column: x => x.venda_id,
                    principalTable: "vendas",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_itens_venda_venda_id",
            table: "itens_venda",
            column: "venda_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "itens_venda");

        migrationBuilder.DropTable(
            name: "vendas");
    }
}
