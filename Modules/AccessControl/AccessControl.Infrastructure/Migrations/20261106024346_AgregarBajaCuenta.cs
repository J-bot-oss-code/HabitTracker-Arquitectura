using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarBajaCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BajaProgramadaPara",
                table: "usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BajaSolicitadaEn",
                table: "usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DesactivadaPorUsuarioEn",
                table: "usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_BajaProgramadaPara",
                table: "usuarios",
                column: "BajaProgramadaPara");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_BajaProgramadaPara",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "BajaProgramadaPara",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "BajaSolicitadaEn",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "DesactivadaPorUsuarioEn",
                table: "usuarios");
        }
    }
}
