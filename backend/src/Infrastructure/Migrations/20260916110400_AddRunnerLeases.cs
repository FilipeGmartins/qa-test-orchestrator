using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRunnerLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancellationRequested",
                table: "TestRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                table: "TestRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseId",
                table: "TestRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressJson",
                table: "TestRuns",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ResultJson",
                table: "TestRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RunnerError",
                table: "TestRuns",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_Status_LeaseExpiresAt",
                table: "TestRuns",
                columns: new[] { "Status", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestRuns_Status_LeaseExpiresAt",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "CancellationRequested",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "LeaseId",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "ProgressJson",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "ResultJson",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "RunnerError",
                table: "TestRuns");
        }
    }
}
