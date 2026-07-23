using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProphetOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoidAndAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Expenses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Expenses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedBy",
                table: "Expenses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Bookings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Bookings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedBy",
                table: "Bookings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", nullable: false),
                    ActorName = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityCode = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_At",
                table: "AuditEntries",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityCode",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "Bookings");
        }
    }
}
