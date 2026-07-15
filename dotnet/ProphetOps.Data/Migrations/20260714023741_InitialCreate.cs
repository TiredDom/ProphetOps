using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProphetOps.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    RelatedPackage = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TravelPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    PackageName = table.Column<string>(type: "TEXT", nullable: false),
                    Destination = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<string>(type: "TEXT", nullable: true),
                    BasePrice = table.Column<int>(type: "INTEGER", nullable: false),
                    Inclusions = table.Column<string>(type: "TEXT", nullable: true),
                    AvailableSlots = table.Column<int>(type: "INTEGER", nullable: false),
                    SoldCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReservedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdatedAt = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    BookingDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PassengerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Client = table.Column<string>(type: "TEXT", nullable: false),
                    TravelPackageId = table.Column<int>(type: "INTEGER", nullable: true),
                    PackageName = table.Column<string>(type: "TEXT", nullable: false),
                    PackageCode = table.Column<string>(type: "TEXT", nullable: true),
                    EntryType = table.Column<string>(type: "TEXT", nullable: false),
                    Destination = table.Column<string>(type: "TEXT", nullable: false),
                    GrossRevenue = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentStatus = table.Column<string>(type: "TEXT", nullable: false),
                    BookingStatus = table.Column<string>(type: "TEXT", nullable: false),
                    StaffAssigned = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_TravelPackages_TravelPackageId",
                        column: x => x.TravelPackageId,
                        principalTable: "TravelPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Code",
                table: "Bookings",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TravelPackageId",
                table: "Bookings",
                column: "TravelPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Code",
                table: "Expenses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TravelPackages_Code",
                table: "TravelPackages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "TravelPackages");
        }
    }
}
