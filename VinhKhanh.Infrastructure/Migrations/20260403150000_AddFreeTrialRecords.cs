using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VinhKhanh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFreeTrialRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FreeTrialRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PoiId = table.Column<int>(type: "int", nullable: false),
                    FirstHeardAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeTrialRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FreeTrialRecords_UserId_PoiId",
                table: "FreeTrialRecords",
                columns: new[] { "UserId", "PoiId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FreeTrialRecords_DeviceId_PoiId",
                table: "FreeTrialRecords",
                columns: new[] { "DeviceId", "PoiId" },
                unique: true,
                filter: "[DeviceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FreeTrialRecords");
        }
    }
}
