using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LimitarCodigosPorCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CodigosEmitidos",
                table: "usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RegistroRepetido",
                table: "usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VentanaCodigosDesde",
                table: "usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorreosEnCola_Estado_FechaCreacion",
                table: "CorreosEnCola",
                columns: new[] { "Estado", "FechaCreacion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CorreosEnCola_Estado_FechaCreacion",
                table: "CorreosEnCola");

            migrationBuilder.DropColumn(
                name: "CodigosEmitidos",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "RegistroRepetido",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "VentanaCodigosDesde",
                table: "usuarios");
        }
    }
}
