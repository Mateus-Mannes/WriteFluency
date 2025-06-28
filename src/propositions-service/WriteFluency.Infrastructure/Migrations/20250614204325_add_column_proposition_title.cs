using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriteFluency.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class add_column_proposition_title : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Propositions",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Propositions");
        }
    }
}
