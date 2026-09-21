using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRunPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PresetId",
                table: "TestRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PresetRevision",
                table: "TestRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RunPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunPresets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunPresets_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PresetRevisions",
                columns: table => new
                {
                    PresetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresetRevisions", x => new { x.PresetId, x.Revision });
                    table.ForeignKey(
                        name: "FK_PresetRevisions_RunPresets_PresetId",
                        column: x => x.PresetId,
                        principalTable: "RunPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_PresetId_PresetRevision",
                table: "TestRuns",
                columns: new[] { "PresetId", "PresetRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_RunPresets_ProjectId_Archived_UpdatedAt_Id",
                table: "RunPresets",
                columns: new[] { "ProjectId", "Archived", "UpdatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_TestRuns_PresetRevisions_PresetId_PresetRevision",
                table: "TestRuns",
                columns: new[] { "PresetId", "PresetRevision" },
                principalTable: "PresetRevisions",
                principalColumns: new[] { "PresetId", "Revision" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestRuns_PresetRevisions_PresetId_PresetRevision",
                table: "TestRuns");

            migrationBuilder.DropTable(
                name: "PresetRevisions");

            migrationBuilder.DropTable(
                name: "RunPresets");

            migrationBuilder.DropIndex(
                name: "IX_TestRuns_PresetId_PresetRevision",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "PresetId",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "PresetRevision",
                table: "TestRuns");
        }
    }
}
