using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VinhKhanh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPoiStatusOwnerPremium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Pois",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPremium",
                table: "Pois",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Pois",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Pois",
                type: "nvarchar(max)",
                nullable: true);

            // CategoryCode có thể đã tồn tại từ script SQL thủ công trước đó
            migrationBuilder.Sql(@"
IF COL_LENGTH('Pois', 'CategoryCode') IS NULL
BEGIN
    ALTER TABLE [Pois] ADD [CategoryCode] NVARCHAR(32) NOT NULL CONSTRAINT [DF_Pois_CategoryCode2] DEFAULT 'FOOD_STREET';
END");

            // Data migration: map IsApproved → Status
            migrationBuilder.Sql(
                "UPDATE Pois SET Status = CASE WHEN IsApproved = 1 THEN 2 ELSE 1 END");

            migrationBuilder.CreateIndex(
                name: "IX_Pois_OwnerId",
                table: "Pois",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pois_AspNetUsers_OwnerId",
                table: "Pois",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pois_AspNetUsers_OwnerId",
                table: "Pois");

            migrationBuilder.DropIndex(
                name: "IX_Pois_OwnerId",
                table: "Pois");

            migrationBuilder.DropColumn(name: "Status", table: "Pois");
            migrationBuilder.DropColumn(name: "IsPremium", table: "Pois");
            migrationBuilder.DropColumn(name: "OwnerId", table: "Pois");
            migrationBuilder.DropColumn(name: "RejectionReason", table: "Pois");
            // CategoryCode không drop vì có thể được tạo bởi script SQL thủ công
        }
    }
}
