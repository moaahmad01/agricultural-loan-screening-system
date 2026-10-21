using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriculturalLoanSystem.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// موديل 5 — DB-06: إضافة فهارس على AuditLogs.Timestamp وAuditLogs.UserId. لم يكن على هذا
    /// الجدول أي فهرس عدا المفتاح الأساسي، رغم أنه ينمو مع كل عملية حفظ تقريبًا في النظام (بسبب
    /// AuditInterceptor العام)، وأن GetAuditLogsAsync يستعلم دومًا بالترتيب على Timestamp وأصبح
    /// يدعم الفلترة بـ UserId أيضًا (DB-07).
    /// </summary>
    public partial class Module5AuditLogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs");
        }
    }
}
