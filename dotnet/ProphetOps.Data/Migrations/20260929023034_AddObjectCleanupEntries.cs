using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProphetOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddObjectCleanupEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObjectCleanupEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ObjectKey = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EligibleAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectCleanupEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObjectCleanupEntries_EligibleAtUtc",
                table: "ObjectCleanupEntries",
                column: "EligibleAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectCleanupEntries_ObjectKey",
                table: "ObjectCleanupEntries",
                column: "ObjectKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObjectCleanupEntries");
        }
    }
}
