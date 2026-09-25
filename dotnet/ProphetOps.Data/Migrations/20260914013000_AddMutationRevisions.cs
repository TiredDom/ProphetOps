using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProphetOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMutationRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "TravelPackages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Bookings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Revision",
                table: "TravelPackages");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Bookings");
        }
    }
}
