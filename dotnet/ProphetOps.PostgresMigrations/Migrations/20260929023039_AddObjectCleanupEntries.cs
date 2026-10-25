using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProphetOps.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddObjectCleanupEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObjectCleanupEntries",
                schema: "prophetops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EligibleAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectCleanupEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObjectCleanupEntries_EligibleAtUtc",
                schema: "prophetops",
                table: "ObjectCleanupEntries",
                column: "EligibleAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectCleanupEntries_ObjectKey",
                schema: "prophetops",
                table: "ObjectCleanupEntries",
                column: "ObjectKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObjectCleanupEntries",
                schema: "prophetops");
        }
    }
}
