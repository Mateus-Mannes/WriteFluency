using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriteFluency.Data;

#nullable disable

namespace WriteFluency.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260717121000_AddAnonymousClientIpToCatalogTeaser")]
    public partial class AddAnonymousClientIpToCatalogTeaser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnonymousClientIpAddress",
                table: "CatalogExerciseGrants",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnonymousClientIpAddress",
                table: "CatalogAccessCounters",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnonymousClientIpAddress",
                table: "CatalogExerciseGrants");

            migrationBuilder.DropColumn(
                name: "AnonymousClientIpAddress",
                table: "CatalogAccessCounters");
        }
    }
}
