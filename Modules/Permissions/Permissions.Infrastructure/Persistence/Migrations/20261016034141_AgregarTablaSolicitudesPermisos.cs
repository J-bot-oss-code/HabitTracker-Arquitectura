using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Permissions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTablaSolicitudesPermisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudesPermisos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Solicitante = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PermisoSolicitado = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Aprobador = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesPermisos", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudesPermisos");
        }
    }
}
