using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VinhKhanh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSchemaAndAddPoiRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Pois', 'CategoryCode') IS NULL ALTER TABLE [Pois] ADD [CategoryCode] NVARCHAR(MAX) NOT NULL DEFAULT '';
IF COL_LENGTH('Pois', 'ImageUrl') IS NULL ALTER TABLE [Pois] ADD [ImageUrl] NVARCHAR(MAX) NULL;
IF COL_LENGTH('Pois', 'IsPremium') IS NULL ALTER TABLE [Pois] ADD [IsPremium] BIT NOT NULL DEFAULT 0;
IF COL_LENGTH('Pois', 'OwnerId') IS NULL ALTER TABLE [Pois] ADD [OwnerId] NVARCHAR(450) NULL;
IF COL_LENGTH('Pois', 'QrToken') IS NULL ALTER TABLE [Pois] ADD [QrToken] NVARCHAR(128) NULL;
IF COL_LENGTH('Pois', 'RejectionReason') IS NULL ALTER TABLE [Pois] ADD [RejectionReason] NVARCHAR(MAX) NULL;
IF COL_LENGTH('Pois', 'Status') IS NULL ALTER TABLE [Pois] ADD [Status] INT NOT NULL DEFAULT 0;
IF COL_LENGTH('AnalyticsEvents', 'EventType') IS NULL ALTER TABLE [AnalyticsEvents] ADD [EventType] NVARCHAR(MAX) NULL;
IF COL_LENGTH('AnalyticsEvents', 'PoiId') IS NULL ALTER TABLE [AnalyticsEvents] ADD [PoiId] INT NULL;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('DeviceTrials', 'U') IS NULL
CREATE TABLE [DeviceTrials] (
    [DeviceId] nvarchar(450) NOT NULL,
    [TrialStartDate] datetime2 NOT NULL,
    [ExpiryDate] datetime2 NOT NULL,
    [LastCheckedAt] datetime2 NULL,
    CONSTRAINT [PK_DeviceTrials] PRIMARY KEY ([DeviceId])
);

IF OBJECT_ID('FreeTrialRecords', 'U') IS NULL
CREATE TABLE [FreeTrialRecords] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NULL,
    [DeviceId] nvarchar(450) NULL,
    [PoiId] int NOT NULL,
    [FirstHeardAt] datetime2 NOT NULL,
    CONSTRAINT [PK_FreeTrialRecords] PRIMARY KEY ([Id])
);

IF OBJECT_ID('Payments', 'U') IS NULL
CREATE TABLE [Payments] (
    [Id] int NOT NULL IDENTITY,
    [TransactionId] nvarchar(450) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Type] int NOT NULL,
    [Status] int NOT NULL,
    [ExpiryDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Payments_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

IF OBJECT_ID('PoiRatings', 'U') IS NULL
CREATE TABLE [PoiRatings] (
    [Id] int NOT NULL IDENTITY,
    [PoiId] int NOT NULL,
    [DeviceId] nvarchar(450) NOT NULL,
    [Stars] int NOT NULL,
    [RatedAt] datetime2 NOT NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    CONSTRAINT [PK_PoiRatings] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PoiRatings_Stars] CHECK ([Stars] >= 1 AND [Stars] <= 5),
    CONSTRAINT [FK_PoiRatings_Pois_PoiId] FOREIGN KEY ([PoiId]) REFERENCES [Pois] ([Id]) ON DELETE CASCADE
);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Pois_OwnerId' AND object_id = OBJECT_ID('Pois'))
CREATE INDEX [IX_Pois_OwnerId] ON [Pois] ([OwnerId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FreeTrialRecords_DeviceId_PoiId' AND object_id = OBJECT_ID('FreeTrialRecords'))
CREATE UNIQUE INDEX [IX_FreeTrialRecords_DeviceId_PoiId] ON [FreeTrialRecords] ([DeviceId], [PoiId]) WHERE [DeviceId] IS NOT NULL;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FreeTrialRecords_UserId_PoiId' AND object_id = OBJECT_ID('FreeTrialRecords'))
CREATE UNIQUE INDEX [IX_FreeTrialRecords_UserId_PoiId] ON [FreeTrialRecords] ([UserId], [PoiId]) WHERE [UserId] IS NOT NULL;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_TransactionId' AND object_id = OBJECT_ID('Payments'))
CREATE UNIQUE INDEX [IX_Payments_TransactionId] ON [Payments] ([TransactionId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_UserId' AND object_id = OBJECT_ID('Payments'))
CREATE INDEX [IX_Payments_UserId] ON [Payments] ([UserId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PoiRatings_DeviceId_PoiId' AND object_id = OBJECT_ID('PoiRatings'))
CREATE UNIQUE INDEX [IX_PoiRatings_DeviceId_PoiId] ON [PoiRatings] ([DeviceId], [PoiId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PoiRatings_PoiId' AND object_id = OBJECT_ID('PoiRatings'))
CREATE INDEX [IX_PoiRatings_PoiId] ON [PoiRatings] ([PoiId]);

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Pois_AspNetUsers_OwnerId')
ALTER TABLE [Pois] ADD CONSTRAINT [FK_Pois_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pois_AspNetUsers_OwnerId",
                table: "Pois");

            migrationBuilder.DropTable(
                name: "DeviceTrials");

            migrationBuilder.DropTable(
                name: "FreeTrialRecords");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PoiRatings");

            migrationBuilder.DropIndex(
                name: "IX_Pois_OwnerId",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "CategoryCode",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "IsPremium",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "QrToken",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "AnalyticsEvents");

            migrationBuilder.DropColumn(
                name: "PoiId",
                table: "AnalyticsEvents");
        }
    }
}
