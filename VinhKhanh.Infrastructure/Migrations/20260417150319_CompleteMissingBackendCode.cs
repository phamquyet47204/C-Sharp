using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VinhKhanh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMissingBackendCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nội dung di trú này đã được thực hiện thủ công qua script SQL trong migration trước đó (FixSchemaAndAddPoiRatings).
            // Do đó, chúng ta để trống phương thức Up để tránh lỗi khi áp dụng lại.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceTrials");

            migrationBuilder.DropTable(
                name: "PoiRatings");

            migrationBuilder.AlterColumn<string>(
                name: "CategoryCode",
                table: "Pois",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "FOOD_STREET",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                table: "Payments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "FreeTrialRecords",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "AnalyticsEvents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
