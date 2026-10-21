using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriculturalLoanSystem.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// موديل 3 — إصلاحات سلامة قاعدة البيانات الخاصة بالفحص والتقييم:
    /// DB-02: منع الحذف المتسلسل (Cascade) لـ Section→Question وQuestion→ScreeningAnswer، واستبداله
    ///        بـ Restrict (يمنع الحذف الفعلي طالما توجد بيانات مرتبطة؛ الطبقة التطبيقية في
    ///        AdminService تتحقق مسبقًا وتستخدم تعطيلاً منطقيًا IsActive=false بدلاً من ذلك).
    /// DB-03: فهرس فريد جديد يمنع إجابتين لنفس السؤال في نفس جلسة الفحص.
    /// DB-04: تحويل فهرسي التفرّد على DisplayOrder (للأقسام والأسئلة) إلى فهارس مُرشَّحة تستثني
    ///        السجلات المعطَّلة منطقيًا (IsActive=0)، للسماح بإعادة استخدام ترتيب قسم/سؤال مُعطَّل.
    /// DB-05: حذف العمود الميت ScreeningSessions.UserId (int) غير المستخدم في أي مكان بالتطبيق.
    /// </summary>
    public partial class Module3ScreeningIntegrityFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== DB-05: حذف العمود الميت ScreeningSessions.UserId =====
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ScreeningSessions");

            // ===== DB-02: Question -> Section: Cascade إلى Restrict =====
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Sections_SectionId",
                table: "Questions");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Sections_SectionId",
                table: "Questions",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ===== DB-02: ScreeningAnswer -> Question: Cascade إلى Restrict =====
            migrationBuilder.DropForeignKey(
                name: "FK_ScreeningAnswers_Questions_QuestionId",
                table: "ScreeningAnswers");

            migrationBuilder.AddForeignKey(
                name: "FK_ScreeningAnswers_Questions_QuestionId",
                table: "ScreeningAnswers",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ===== DB-04: فهرس Questions(SectionId, DisplayOrder) يصبح مُرشَّحًا =====
            migrationBuilder.DropIndex(
                name: "IX_Questions_SectionId_DisplayOrder",
                table: "Questions");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SectionId_DisplayOrder",
                table: "Questions",
                columns: new[] { "SectionId", "DisplayOrder" },
                unique: true,
                filter: "[IsActive] = 1");

            // ===== DB-04: فهرس Sections(DisplayOrder) يصبح مُرشَّحًا =====
            migrationBuilder.DropIndex(
                name: "IX_Sections_DisplayOrder",
                table: "Sections");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DisplayOrder",
                table: "Sections",
                column: "DisplayOrder",
                unique: true,
                filter: "[IsActive] = 1");

            // ===== DB-03: فهرس فريد جديد يمنع تكرار الإجابة لنفس السؤال بنفس الجلسة =====
            // ملاحظة تشغيلية مهمة: لو كانت قاعدة البيانات الحالية تحتوي بالفعل على صفوف مكررة
            // (SessionId, QuestionId) من قبل هذا الإصلاح (بسبب غياب القيد سابقًا)، فإن إنشاء هذا
            // الفهرس الفريد سيفشل عند التطبيق. في هذه الحالة يجب تنظيف التكرارات يدويًا أولاً، مثلاً:
            //   ;WITH d AS (
            //       SELECT Id, ROW_NUMBER() OVER (PARTITION BY SessionId, QuestionId ORDER BY Id DESC) rn
            //       FROM ScreeningAnswers)
            //   DELETE FROM ScreeningAnswers WHERE Id IN (SELECT Id FROM d WHERE rn > 1);
            migrationBuilder.CreateIndex(
                name: "IX_ScreeningAnswers_SessionId_QuestionId",
                table: "ScreeningAnswers",
                columns: new[] { "SessionId", "QuestionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScreeningAnswers_SessionId_QuestionId",
                table: "ScreeningAnswers");

            migrationBuilder.DropIndex(
                name: "IX_Sections_DisplayOrder",
                table: "Sections");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DisplayOrder",
                table: "Sections",
                column: "DisplayOrder",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Questions_SectionId_DisplayOrder",
                table: "Questions");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SectionId_DisplayOrder",
                table: "Questions",
                columns: new[] { "SectionId", "DisplayOrder" },
                unique: true);

            migrationBuilder.DropForeignKey(
                name: "FK_ScreeningAnswers_Questions_QuestionId",
                table: "ScreeningAnswers");

            migrationBuilder.AddForeignKey(
                name: "FK_ScreeningAnswers_Questions_QuestionId",
                table: "ScreeningAnswers",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Sections_SectionId",
                table: "Questions");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Sections_SectionId",
                table: "Questions",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ScreeningSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
