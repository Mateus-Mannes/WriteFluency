using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WriteFluency.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPropositionTrackingAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Propositions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Propositions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PropositionGenerationLogId",
                table: "Propositions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PropositionGenerationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GenerationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    ComplexityId = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropositionGenerationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropositionGenerationLogs_Complexities_ComplexityId",
                        column: x => x.ComplexityId,
                        principalTable: "Complexities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropositionGenerationLogs_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Propositions_IsDeleted",
                table: "Propositions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Propositions_PropositionGenerationLogId",
                table: "Propositions",
                column: "PropositionGenerationLogId");

            migrationBuilder.CreateIndex(
                name: "IX_Propositions_SubjectId_IsDeleted",
                table: "Propositions",
                columns: new[] { "SubjectId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_PropositionGenerationLogs_ComplexityId",
                table: "PropositionGenerationLogs",
                column: "ComplexityId");

            migrationBuilder.CreateIndex(
                name: "IX_PropositionGenerationLogs_SubjectId_ComplexityId_CreatedAt",
                table: "PropositionGenerationLogs",
                columns: new[] { "SubjectId", "ComplexityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PropositionGenerationLogs_SubjectId_ComplexityId_Generation~",
                table: "PropositionGenerationLogs",
                columns: new[] { "SubjectId", "ComplexityId", "GenerationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PropositionGenerationLogs_SubjectId_ComplexityId_Success",
                table: "PropositionGenerationLogs",
                columns: new[] { "SubjectId", "ComplexityId", "Success" });

            migrationBuilder.AddForeignKey(
                name: "FK_Propositions_PropositionGenerationLogs_PropositionGeneratio~",
                table: "Propositions",
                column: "PropositionGenerationLogId",
                principalTable: "PropositionGenerationLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Propositions_PropositionGenerationLogs_PropositionGeneratio~",
                table: "Propositions");

            migrationBuilder.DropTable(
                name: "PropositionGenerationLogs");

            migrationBuilder.DropIndex(
                name: "IX_Propositions_IsDeleted",
                table: "Propositions");

            migrationBuilder.DropIndex(
                name: "IX_Propositions_PropositionGenerationLogId",
                table: "Propositions");

            migrationBuilder.DropIndex(
                name: "IX_Propositions_SubjectId_IsDeleted",
                table: "Propositions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Propositions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Propositions");

            migrationBuilder.DropColumn(
                name: "PropositionGenerationLogId",
                table: "Propositions");
        }
    }
}
