using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using WriteFluency.Data;

#nullable disable

namespace WriteFluency.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260706150000_AddAiUsageCounters")]
    public partial class AddAiUsageCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Feature = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PeriodKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReservedRequestCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedRequestCount = table.Column<int>(type: "integer", nullable: false),
                    FailedRequestCount = table.Column<int>(type: "integer", nullable: false),
                    InputTokenCount = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokenCount = table.Column<long>(type: "bigint", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageCounters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageCounters_UserId_Feature_PeriodKind_PeriodKey",
                table: "AiUsageCounters",
                columns: new[] { "UserId", "Feature", "PeriodKind", "PeriodKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageCounters");
        }
    }
}
