using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCodigoRecuperacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Recuperacion_FechaEmision",
                table: "usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Recuperacion_FechaVencimiento",
                table: "usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Recuperacion_Usado",
                table: "usuarios",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recuperacion_Valor",
                table: "usuarios",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Recuperacion_FechaEmision",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "Recuperacion_FechaVencimiento",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "Recuperacion_Usado",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "Recuperacion_Valor",
                table: "usuarios");
        }
    }
}
