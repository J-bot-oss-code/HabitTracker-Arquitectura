using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTablaDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TipoContenido = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    FechaSubidaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorreoUsuario = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NombreAlmacenado = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    FechaEliminacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_UsuarioId_Eliminado",
                table: "Documentos",
                columns: new[] { "UsuarioId", "Eliminado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documentos");
        }
    }
}
