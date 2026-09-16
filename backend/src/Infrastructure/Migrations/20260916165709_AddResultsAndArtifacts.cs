using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResultsAndArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArtifactsPurgedAt",
                table: "TestRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TestAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StableKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Browser = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Stack = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Logs = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestAttempts_TestCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestAttempts_TestRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "TestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestArtifacts_TestAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "TestAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestArtifacts_TestRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "TestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_ArtifactsPurgedAt_FinishedAt",
                table: "TestRuns",
                columns: new[] { "ArtifactsPurgedAt", "FinishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TestArtifacts_AttemptId",
                table: "TestArtifacts",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_TestArtifacts_RunId",
                table: "TestArtifacts",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_CaseId_RecordedAt_Id",
                table: "TestAttempts",
                columns: new[] { "CaseId", "RecordedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_RunId_CaseId_Browser_Attempt",
                table: "TestAttempts",
                columns: new[] { "RunId", "CaseId", "Browser", "Attempt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestArtifacts");

            migrationBuilder.DropTable(
                name: "TestAttempts");

            migrationBuilder.DropIndex(
                name: "IX_TestRuns_ArtifactsPurgedAt_FinishedAt",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "ArtifactsPurgedAt",
                table: "TestRuns");
        }
    }
}
