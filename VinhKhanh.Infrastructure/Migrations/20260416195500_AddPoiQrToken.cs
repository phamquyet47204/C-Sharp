using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VinhKhanh.Infrastructure.Migrations
{
    public partial class AddPoiQrToken : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrToken",
                table: "Pois",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QrToken",
                table: "Pois");
        }
    }
}
