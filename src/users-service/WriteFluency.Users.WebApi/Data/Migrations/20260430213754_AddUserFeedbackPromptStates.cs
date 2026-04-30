using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriteFluency.Users.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFeedbackPromptStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserFeedbackPromptStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CampaignKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastShownAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastDismissedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DismissCount = table.Column<int>(type: "integer", nullable: false),
                    SubmitCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFeedbackPromptStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFeedbackPromptStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFeedbackPromptStates_UserId_CampaignKey",
                table: "UserFeedbackPromptStates",
                columns: new[] { "UserId", "CampaignKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFeedbackPromptStates");
        }
    }
}
