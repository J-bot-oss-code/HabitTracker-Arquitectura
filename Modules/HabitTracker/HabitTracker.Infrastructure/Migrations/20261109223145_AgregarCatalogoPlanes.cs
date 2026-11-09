using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCatalogoPlanes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanesPlantilla",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Resumen = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nivel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Intensidad = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Sexo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EdadMinima = table.Column<int>(type: "int", nullable: false),
                    EdadMaxima = table.Column<int>(type: "int", nullable: false),
                    Semanas = table.Column<int>(type: "int", nullable: false),
                    PerfilesLaborales = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lugar = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Impacto = table.Column<bool>(type: "bit", nullable: false),
                    ZonasCorporales = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contraindicaciones = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Alergenos = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Beneficios = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AvisoSeguridad = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequiereCuestionario = table.Column<bool>(type: "bit", nullable: false),
                    VersionContenido = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    GuiaSemanal = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesPlantilla", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TareasPlantilla",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Instrucciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Periodicidad = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Unidad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VersionMinima = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AnclaSugerida = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlanPlantillaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TareasPlantilla", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TareasPlantilla_PlanesPlantilla_PlanPlantillaId",
                        column: x => x.PlanPlantillaId,
                        principalTable: "PlanesPlantilla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesPlantilla_Activo",
                table: "PlanesPlantilla",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_PlanesPlantilla_Slug",
                table: "PlanesPlantilla",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TareasPlantilla_PlanPlantillaId",
                table: "TareasPlantilla",
                column: "PlanPlantillaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TareasPlantilla");

            migrationBuilder.DropTable(
                name: "PlanesPlantilla");
        }
    }
}
