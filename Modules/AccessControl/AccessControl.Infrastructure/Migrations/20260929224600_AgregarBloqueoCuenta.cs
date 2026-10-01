using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarBloqueoCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Bloqueo_BloqueadoHasta",
                table: "usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bloqueo_IntentosFallidos",
                table: "usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bloqueo_BloqueadoHasta",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "Bloqueo_IntentosFallidos",
                table: "usuarios");
        }
    }
}
