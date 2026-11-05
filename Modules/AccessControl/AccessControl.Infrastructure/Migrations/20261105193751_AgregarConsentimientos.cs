using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConsentimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentimientosUsuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TerminosVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PrivacidadVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DatosSalud = table.Column<bool>(type: "bit", nullable: false),
                    Marketing = table.Column<bool>(type: "bit", nullable: false),
                    CompartirCumplimiento = table.Column<bool>(type: "bit", nullable: false),
                    EstadisticasAnonimas = table.Column<bool>(type: "bit", nullable: false),
                    AceptadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentimientosUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentimientosUsuario_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentimientosUsuario_UsuarioId_ActualizadoEn",
                table: "ConsentimientosUsuario",
                columns: new[] { "UsuarioId", "ActualizadoEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentimientosUsuario");
        }
    }
}
