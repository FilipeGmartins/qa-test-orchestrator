using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPageAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPageAudit",
                table: "TestSuites",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TestSuites_PageAuditProject",
                table: "TestSuites",
                column: "ProjectId",
                unique: true,
                filter: "\"IsPageAudit\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestSuites_PageAuditProject",
                table: "TestSuites");

            migrationBuilder.DropColumn(
                name: "IsPageAudit",
                table: "TestSuites");
        }
    }
}
