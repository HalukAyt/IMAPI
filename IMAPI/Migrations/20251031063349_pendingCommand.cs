using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMAPI.Migrations
{
    /// <inheritdoc />
    public partial class pendingCommand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceSerial = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingCommands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingCommands_DeviceSerial_Status",
                table: "PendingCommands",
                columns: new[] { "DeviceSerial", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingCommands_ExpiresAt",
                table: "PendingCommands",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingCommands");
        }
    }
}
