using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrigenDelCodigoDeRecuperacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Recuperacion_IniciadoPorAdministrador",
                table: "usuarios",
                type: "bit",
                nullable: true);

            // Un código que ya estaba vigente lo pidió la propia persona (el forzado por un Administrador aún no dejaba huella):
            // sin esto, EF leería NULL en un bool no anulable al cargar a quien lo tenga.
            migrationBuilder.Sql("UPDATE usuarios SET Recuperacion_IniciadoPorAdministrador = 0 WHERE Recuperacion_Valor IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Recuperacion_IniciadoPorAdministrador",
                table: "usuarios");
        }
    }
}
