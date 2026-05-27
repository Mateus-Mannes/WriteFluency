using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WriteFluency.Users.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionEntitlementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SubscriptionCancelAtPeriodEnd",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubscriptionCurrentPeriodEndUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionPlan",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "free");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionCancelAtPeriodEnd",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SubscriptionCurrentPeriodEndUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlan",
                table: "AspNetUsers");
        }
    }
}
