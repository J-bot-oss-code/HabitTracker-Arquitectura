using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QuitarMetaSlugDelPerfil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InicioPlan",
                table: "PerfilesUsuario");

            migrationBuilder.DropColumn(
                name: "MetaSlug",
                table: "PerfilesUsuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InicioPlan",
                table: "PerfilesUsuario",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaSlug",
                table: "PerfilesUsuario",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);
        }
    }
}
