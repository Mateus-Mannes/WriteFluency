using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriteFluency.Data;

#nullable disable

namespace WriteFluency.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260505120000_AddRequestedPublishedBeforeToGenerationLogs")]
    public partial class AddRequestedPublishedBeforeToGenerationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedPublishedBefore",
                table: "PropositionGenerationLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "PropositionGenerationLogs"
                SET "RequestedPublishedBefore" = "GenerationDate"
                WHERE "RequestedPublishedBefore" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestedPublishedBefore",
                table: "PropositionGenerationLogs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropositionGenerationLogs_SubjectId_ComplexityId_Requested~",
                table: "PropositionGenerationLogs",
                columns: new[] { "SubjectId", "ComplexityId", "RequestedPublishedBefore" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropositionGenerationLogs_SubjectId_ComplexityId_Requested~",
                table: "PropositionGenerationLogs");

            migrationBuilder.DropColumn(
                name: "RequestedPublishedBefore",
                table: "PropositionGenerationLogs");
        }
    }
}
