using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriculturalLoanSystem.Migrations
{
    /// <inheritdoc />
    public partial class AlignAuditStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScreeningAnswers_SessionId",
                table: "ScreeningAnswers");

            // Run before the existing audit-index migration on a clean database.
            // Preserve an already-existing index when upgrading an older deployment.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM AuditLogs WHERE LEN(UserId) > 450)
                    THROW 51000, 'Audit user IDs exceed 450 characters; review existing data before migrating.', 1;
                DECLARE @hadIndex bit = CASE WHEN EXISTS (
                    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AuditLogs')
                    AND name = 'IX_AuditLogs_UserId') THEN 1 ELSE 0 END;
                IF @hadIndex = 1 DROP INDEX IX_AuditLogs_UserId ON AuditLogs;
                ALTER TABLE AuditLogs ALTER COLUMN UserId nvarchar(450) NULL;
                IF @hadIndex = 1 CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE AuditLogs ALTER COLUMN UserId nvarchar(max) NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningAnswers_SessionId",
                table: "ScreeningAnswers",
                column: "SessionId");
        }
    }
}
