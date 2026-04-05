using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VinhKhanh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsEventPoiId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PoiId",
                table: "AnalyticsEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "AnalyticsEvents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PoiId", table: "AnalyticsEvents");
            migrationBuilder.DropColumn(name: "EventType", table: "AnalyticsEvents");
        }
    }
}
