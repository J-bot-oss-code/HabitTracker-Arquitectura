using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ActualizarEsquemaHabitos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Completado",
                table: "RegistrosDiarios");

            migrationBuilder.DropColumn(
                name: "Notas",
                table: "RegistrosDiarios");

            migrationBuilder.DropColumn(
                name: "Frecuencia",
                table: "Habitos");

            migrationBuilder.AddColumn<decimal>(
                name: "Cantidad",
                table: "RegistrosDiarios",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Unidad",
                table: "RegistrosDiarios",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CantidadObjetivo",
                table: "Habitos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicioCiclo",
                table: "Habitos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Periodicidad",
                table: "Habitos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioId",
                table: "Habitos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cantidad",
                table: "RegistrosDiarios");

            migrationBuilder.DropColumn(
                name: "Unidad",
                table: "RegistrosDiarios");

            migrationBuilder.DropColumn(
                name: "CantidadObjetivo",
                table: "Habitos");

            migrationBuilder.DropColumn(
                name: "FechaInicioCiclo",
                table: "Habitos");

            migrationBuilder.DropColumn(
                name: "Periodicidad",
                table: "Habitos");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Habitos");

            migrationBuilder.AddColumn<string>(
                name: "Frecuencia",
                table: "Habitos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Completado",
                table: "RegistrosDiarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notas",
                table: "RegistrosDiarios",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
