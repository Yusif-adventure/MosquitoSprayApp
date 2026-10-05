using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMosquitoControl.Data.Migrations
{
    /// <summary>
    /// Adds the device command queue, scheduler bookkeeping, per-device API keys and
    /// globally unique sprayer IDs; removes columns that are now derived or unused.
    ///
    /// NOTE: the unique indexes on DeviceId will fail to create if an existing database already
    /// contains the same sprayer ID for two accounts. Resolve those rows before upgrading.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261004190000_DeviceCommandsAndScheduler")]
    public partial class DeviceCommandsAndScheduler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_UserId_DeviceId",
                table: "Devices");

            // EF Core's SQLite provider refuses to emit ALTER TABLE ... DROP COLUMN,
            // but the bundled SQLite engine (3.35+) supports it natively.
            migrationBuilder.Sql(@"ALTER TABLE ""AspNetUsers"" DROP COLUMN ""PreferredDevice"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Devices""     DROP COLUMN ""IsOnline"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Schedules""   DROP COLUMN ""Description"";");

            migrationBuilder.AddColumn<string>(
                name: "ApiKeyHash",
                table: "LinkedDevices",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Schedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutedAt",
                table: "Schedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceCommands_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceId",
                table: "Devices",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                table: "Devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkedDevices_DeviceId",
                table: "LinkedDevices",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_DeviceId_Status",
                table: "DeviceCommands",
                columns: new[] { "DeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_UserId",
                table: "DeviceCommands",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceCommands");

            migrationBuilder.DropIndex(
                name: "IX_Devices_DeviceId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_UserId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_LinkedDevices_DeviceId",
                table: "LinkedDevices");

            // Same story as Up: raw SQL for DROP COLUMN.
            migrationBuilder.Sql(@"ALTER TABLE ""LinkedDevices"" DROP COLUMN ""ApiKeyHash"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Schedules""     DROP COLUMN ""Status"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Schedules""     DROP COLUMN ""ExecutedAt"";");

            migrationBuilder.AddColumn<string>(
                name: "PreferredDevice",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Schedules",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId_DeviceId",
                table: "Devices",
                columns: new[] { "UserId", "DeviceId" },
                unique: true);
        }
    }
}