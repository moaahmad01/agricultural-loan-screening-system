using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriculturalLoanSystem.Migrations
{
    /// <inheritdoc />
    public partial class UniqueLoanPerScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT SessionId FROM LoanCalculations WHERE SessionId IS NOT NULL
                           GROUP BY SessionId HAVING COUNT(*) > 1)
                    THROW 51001, 'Duplicate loans reference one screening; review records before migrating. No data was deleted.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_LoanCalculations_SessionId",
                table: "LoanCalculations");

            migrationBuilder.CreateIndex(
                name: "IX_LoanCalculations_SessionId",
                table: "LoanCalculations",
                column: "SessionId",
                unique: true,
                filter: "[SessionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoanCalculations_SessionId",
                table: "LoanCalculations");

            migrationBuilder.CreateIndex(
                name: "IX_LoanCalculations_SessionId",
                table: "LoanCalculations",
                column: "SessionId");
        }
    }
}
