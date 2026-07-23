using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProphetOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "TravelPackages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "TravelPackages");
        }
    }
}
