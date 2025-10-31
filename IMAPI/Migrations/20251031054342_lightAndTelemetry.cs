using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMAPI.Migrations
{
    /// <inheritdoc />
    public partial class lightAndTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LightChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChNo = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsOn = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LightChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LightChannels_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Telemetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceSerial = table.Column<string>(type: "text", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ts = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Telemetries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LightChannels_DeviceId_ChNo",
                table: "LightChannels",
                columns: new[] { "DeviceId", "ChNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_DeviceSerial",
                table: "Telemetries",
                column: "DeviceSerial");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_Ts",
                table: "Telemetries",
                column: "Ts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LightChannels");

            migrationBuilder.DropTable(
                name: "Telemetries");
        }
    }
}
