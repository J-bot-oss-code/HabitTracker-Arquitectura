using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrigenYClaveDeRegistro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveIdempotencia",
                table: "RegistrosDiarios",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "RegistrosDiarios",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "App");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosDiarios_HabitoId_ClaveIdempotencia",
                table: "RegistrosDiarios",
                columns: new[] { "HabitoId", "ClaveIdempotencia" },
                unique: true,
                filter: "[ClaveIdempotencia] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RegistrosDiarios_HabitoId_ClaveIdempotencia",
                table: "RegistrosDiarios");

            migrationBuilder.DropColumn(
                name: "ClaveIdempotencia",
                table: "RegistrosDiarios");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "RegistrosDiarios");
        }
    }
}
