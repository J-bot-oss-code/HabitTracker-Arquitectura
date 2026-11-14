using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAdjuntosPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdjuntosPlan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjuntadoEnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjuntosPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdjuntosPlan_PlanesUsuario_PlanUsuarioId",
                        column: x => x.PlanUsuarioId,
                        principalTable: "PlanesUsuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdjuntosPlan_PlanUsuarioId_DocumentoId",
                table: "AdjuntosPlan",
                columns: new[] { "PlanUsuarioId", "DocumentoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdjuntosPlan_UsuarioId_DocumentoId",
                table: "AdjuntosPlan",
                columns: new[] { "UsuarioId", "DocumentoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjuntosPlan");
        }
    }
}
