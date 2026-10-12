using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MejorasCalificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Recuperacion_IntentosFallidos",
                table: "usuarios",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimoIntento",
                table: "CorreosEnCola",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Intentos",
                table: "CorreosEnCola",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UltimoError",
                table: "CorreosEnCola",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RevocacionesSesion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaCorte = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevocacionesSesion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokensRevocados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensRevocados", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevocacionesSesion");

            migrationBuilder.DropTable(
                name: "TokensRevocados");

            migrationBuilder.DropColumn(
                name: "Recuperacion_IntentosFallidos",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "FechaUltimoIntento",
                table: "CorreosEnCola");

            migrationBuilder.DropColumn(
                name: "Intentos",
                table: "CorreosEnCola");

            migrationBuilder.DropColumn(
                name: "UltimoError",
                table: "CorreosEnCola");
        }
    }
}
