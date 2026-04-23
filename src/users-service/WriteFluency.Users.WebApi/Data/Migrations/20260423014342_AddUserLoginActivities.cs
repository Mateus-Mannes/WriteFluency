using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriteFluency.Users.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLoginActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserLoginActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AuthMethod = table.Column<string>(type: "text", nullable: false),
                    AuthProvider = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CountryIsoCode = table.Column<string>(type: "text", nullable: true),
                    CountryName = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    GeoLookupStatus = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLoginActivities_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginActivities_OccurredAtUtc",
                table: "UserLoginActivities",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginActivities_UserId_OccurredAtUtc",
                table: "UserLoginActivities",
                columns: new[] { "UserId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLoginActivities");
        }
    }
}
