using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriteFluency.Users.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListenWriteTutorialCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ListenWriteTutorialCompleted",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ListenWriteTutorialCompleted",
                table: "AspNetUsers");
        }
    }
}
