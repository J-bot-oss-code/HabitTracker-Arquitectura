using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSesionesUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SesionesUsuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoUsoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevocadaEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReemplazadaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Dispositivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IpCreacion = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionesUsuario_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SesionesUsuario_FamiliaId",
                table: "SesionesUsuario",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesUsuario_TokenHash",
                table: "SesionesUsuario",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionesUsuario_UsuarioId",
                table: "SesionesUsuario",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SesionesUsuario");
        }
    }
}
