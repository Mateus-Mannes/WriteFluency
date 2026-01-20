using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriteFluency.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioDurationSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudioDurationSeconds",
                table: "Propositions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioDurationSeconds",
                table: "Propositions");
        }
    }
}
