using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VinhKhanh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettingsAndPaymentPoiId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "PoiLocalizations");

            migrationBuilder.AddColumn<int>(
                name: "PoiId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "PoiId",
                table: "Payments");

            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "PoiLocalizations",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
