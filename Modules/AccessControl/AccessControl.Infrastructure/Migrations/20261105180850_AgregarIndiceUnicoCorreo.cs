using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndiceUnicoCorreo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Una base creada antes de este índice puede tener correos repetidos (la unicidad solo se comprobaba en
            // código). No se borra ni se une nada automáticamente: se detiene con un mensaje claro para que se revise a mano.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM usuarios GROUP BY Correo HAVING COUNT(*) > 1) " +
                "THROW 50001, 'Hay correos duplicados en la tabla usuarios. Unifique o elimine los duplicados " +
                "(SELECT Correo, COUNT(*) FROM usuarios GROUP BY Correo HAVING COUNT(*) > 1) antes de crear el indice unico IX_usuarios_Correo.', 1;");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Correo",
                table: "usuarios",
                column: "Correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_Correo",
                table: "usuarios");
        }
    }
}
