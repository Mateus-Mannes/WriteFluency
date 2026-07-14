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
    [Migration("20260710120000_AddCatalogAccessTeaser")]
    public partial class AddCatalogAccessTeaser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogAccessCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Feature = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogAccessCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogExerciseGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PropositionId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogExerciseGrants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogAccessCounters_SubjectType_SubjectKey_Feature",
                table: "CatalogAccessCounters",
                columns: new[] { "SubjectType", "SubjectKey", "Feature" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogExerciseGrants_SubjectType_SubjectKey_PropositionId",
                table: "CatalogExerciseGrants",
                columns: new[] { "SubjectType", "SubjectKey", "PropositionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogAccessCounters");

            migrationBuilder.DropTable(
                name: "CatalogExerciseGrants");
        }
    }
}
