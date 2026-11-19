using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPlanesUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsVersionMinima",
                table: "RegistrosDiarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Ancla",
                table: "Habitos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "Habitos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PausadoPorPlan",
                table: "Habitos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanUsuarioId",
                table: "Habitos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersionMinima",
                table: "Habitos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LogrosUsuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ObtenidoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VistoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogrosUsuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanesUsuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanPlantillaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Semanas = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFinPrevista = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MotivoPausa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ModoSuaveHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Extensiones = table.Column<int>(type: "int", nullable: false),
                    HoraRecordatorio = table.Column<TimeOnly>(type: "time", nullable: true),
                    RecordatoriosActivos = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesUsuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeriodosPausa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodosPausa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodosPausa_PlanesUsuario_PlanUsuarioId",
                        column: x => x.PlanUsuarioId,
                        principalTable: "PlanesUsuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Habitos_PlanUsuarioId",
                table: "Habitos",
                column: "PlanUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_LogrosUsuario_UsuarioId_Clave",
                table: "LogrosUsuario",
                columns: new[] { "UsuarioId", "Clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosPausa_PlanUsuarioId",
                table: "PeriodosPausa",
                column: "PlanUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanesUsuario_UsuarioId_Estado",
                table: "PlanesUsuario",
                columns: new[] { "UsuarioId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesUsuario_UsuarioId_PlanPlantillaId",
                table: "PlanesUsuario",
                columns: new[] { "UsuarioId", "PlanPlantillaId" },
                unique: true,
                filter: "[PlanPlantillaId] IS NOT NULL AND [Estado] IN ('Activo', 'Pausado')");

            migrationBuilder.AddForeignKey(
                name: "FK_Habitos_PlanesUsuario_PlanUsuarioId",
                table: "Habitos",
                column: "PlanUsuarioId",
                principalTable: "PlanesUsuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Habitos_PlanesUsuario_PlanUsuarioId",
                table: "Habitos");

            migrationBuilder.DropTable(
                name: "LogrosUsuario");

            migrationBuilder.DropTable(
                name: "PeriodosPausa");

            migrationBuilder.DropTable(
                name: "PlanesUsuario");

            migrationBuilder.DropIndex(
                name: "IX_Habitos_PlanUsuarioId",
                table: "Habitos");

            migrationBuilder.DropColumn(
                name: "EsVersionMinima",
                table: "RegistrosDiarios");

            migrationBuilder.DropColumn(
                name: "Ancla",
                table: "Habitos");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "Habitos");

            migrationBuilder.DropColumn(
                name: "PausadoPorPlan",
                table: "Habitos");

            migrationBuilder.DropColumn(
                name: "PlanUsuarioId",
                table: "Habitos");

            migrationBuilder.DropColumn(
                name: "VersionMinima",
                table: "Habitos");
        }
    }
}
