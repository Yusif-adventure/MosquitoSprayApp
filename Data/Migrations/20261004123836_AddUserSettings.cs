using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMosquitoControl.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Severity",
                table: "Notifications",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<bool>(
                name: "LowInsecticideAlertsEnabled",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "LowInsecticideThreshold",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "SprayDurationSeconds",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "SprayNotificationsEnabled",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LowInsecticideAlertsEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LowInsecticideThreshold",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SprayDurationSeconds",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SprayNotificationsEnabled",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "Severity",
                table: "Notifications",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }
    }
}
