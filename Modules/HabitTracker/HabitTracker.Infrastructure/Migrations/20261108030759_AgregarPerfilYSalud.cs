using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPerfilYSalud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuestionariosSalud",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ObjetivosCifrado = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ObjetivosRespondidoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SeguridadCifrado = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SeguridadRespondidoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CondicionesCifrado = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CondicionesRespondidoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuestionariosSalud", x => x.UsuarioId);
                });

            migrationBuilder.CreateTable(
                name: "MedicionesCorporales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Zona = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicionesCorporales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerfilesUsuario",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerfilLaboral = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ZonaHoraria = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AvisoSaludAceptado = table.Column<bool>(type: "bit", nullable: false),
                    HoraEntradaTrabajo = table.Column<TimeOnly>(type: "time", nullable: true),
                    HoraSalidaTrabajo = table.Column<TimeOnly>(type: "time", nullable: true),
                    HoraDespertar = table.Column<TimeOnly>(type: "time", nullable: true),
                    HoraDormir = table.Column<TimeOnly>(type: "time", nullable: true),
                    Edad = table.Column<int>(type: "int", nullable: true),
                    Sexo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AlturaCm = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    PesoKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    ObjetivoPesoKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Actividad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DiasPorSemana = table.Column<int>(type: "int", nullable: true),
                    MinutosPorSesion = table.Column<int>(type: "int", nullable: true),
                    MetaSlug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    InicioPlan = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfilesUsuario", x => x.UsuarioId);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosPeso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PesoKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosPeso", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicionesCorporales_UsuarioId_Zona_Fecha",
                table: "MedicionesCorporales",
                columns: new[] { "UsuarioId", "Zona", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosPeso_UsuarioId_Fecha",
                table: "RegistrosPeso",
                columns: new[] { "UsuarioId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuestionariosSalud");

            migrationBuilder.DropTable(
                name: "MedicionesCorporales");

            migrationBuilder.DropTable(
                name: "PerfilesUsuario");

            migrationBuilder.DropTable(
                name: "RegistrosPeso");
        }
    }
}
