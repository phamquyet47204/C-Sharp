using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VinhKhanh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsurePoiRadiusColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Pois', 'Radius') IS NULL
BEGIN
    ALTER TABLE [Pois] ADD [Radius] float NOT NULL CONSTRAINT [DF_Pois_Radius] DEFAULT (50.0)
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Pois', 'Radius') IS NOT NULL
BEGIN
    IF OBJECT_ID('DF_Pois_Radius', 'D') IS NOT NULL
        ALTER TABLE [Pois] DROP CONSTRAINT [DF_Pois_Radius]
    ALTER TABLE [Pois] DROP COLUMN [Radius]
END
");
        }
    }
}
