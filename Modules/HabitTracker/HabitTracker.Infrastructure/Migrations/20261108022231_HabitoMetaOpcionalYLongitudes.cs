using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HabitoMetaOpcionalYLongitudes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Las columnas pasan de nvarchar(max) a tamaños acotados (los mismos que valida la API). No se trunca nada en
            // silencio: si una fila no cabe, la migración se detiene con un mensaje claro para revisarla a mano.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM Habitos WHERE DATALENGTH(Nombre) / 2 > 200 OR DATALENGTH(Unidad) / 2 > 50) " +
                "OR EXISTS (SELECT 1 FROM RegistrosDiarios WHERE DATALENGTH(Unidad) / 2 > 50) " +
                "OR EXISTS (SELECT 1 FROM Metas WHERE DATALENGTH(Titulo) / 2 > 200 OR DATALENGTH(EstadoActual) / 2 > 50) " +
                "OR EXISTS (SELECT 1 FROM Categorias WHERE DATALENGTH(Nombre) / 2 > 100 OR DATALENGTH(Descripcion) / 2 > 500) " +
                "OR EXISTS (SELECT 1 FROM Recompensas WHERE DATALENGTH(Nombre) / 2 > 100) " +
                "OR EXISTS (SELECT 1 FROM Etiquetas WHERE DATALENGTH(Nombre) / 2 > 50 OR DATALENGTH(ColorHex) / 2 > 7) " +
                "THROW 50002, 'Hay textos mas largos que los nuevos limites (Habitos.Nombre 200, Unidad 50, Metas.Titulo 200, " +
                "EstadoActual 50, Categorias.Nombre 100, Descripcion 500, Recompensas.Nombre 100, Etiquetas.Nombre 50, ColorHex 7). " +
                "Acortelos antes de aplicar la migracion HabitoMetaOpcionalYLongitudes.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_Habitos_Metas_MetaId",
                table: "Habitos");

            migrationBuilder.AlterColumn<string>(
                name: "Unidad",
                table: "RegistrosDiarios",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Recompensas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Titulo",
                table: "Metas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "EstadoActual",
                table: "Metas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Unidad",
                table: "Habitos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Habitos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "MetaId",
                table: "Habitos",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Etiquetas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ColorHex",
                table: "Etiquetas",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Categorias",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Categorias",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Habitos_UsuarioId",
                table: "Habitos",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Habitos_Metas_MetaId",
                table: "Habitos",
                column: "MetaId",
                principalTable: "Metas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir vuelve a exigir MetaId. Un hábito sin meta (permitido desde esta migración) no tiene a qué meta
            // apuntar: se detiene con un mensaje claro en lugar de fallar al recrear la clave foránea.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM Habitos WHERE MetaId IS NULL) " +
                "THROW 50003, 'Hay habitos sin meta (MetaId NULL). Asigneles una meta o eliminelos antes de revertir " +
                "la migracion HabitoMetaOpcionalYLongitudes.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_Habitos_Metas_MetaId",
                table: "Habitos");

            migrationBuilder.DropIndex(
                name: "IX_Habitos_UsuarioId",
                table: "Habitos");

            migrationBuilder.AlterColumn<string>(
                name: "Unidad",
                table: "RegistrosDiarios",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Recompensas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Titulo",
                table: "Metas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "EstadoActual",
                table: "Metas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Unidad",
                table: "Habitos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Habitos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "MetaId",
                table: "Habitos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Etiquetas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ColorHex",
                table: "Etiquetas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Categorias",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Categorias",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddForeignKey(
                name: "FK_Habitos_Metas_MetaId",
                table: "Habitos",
                column: "MetaId",
                principalTable: "Metas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
