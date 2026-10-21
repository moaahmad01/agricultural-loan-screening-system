using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Services.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AgriculturalLoanSystem.Tests.Services
{
    /// <summary>
    /// اختبارات انحدار لإصلاحات AdminService (موديل 7 — ج.4). كل اختبار هنا يقابل مباشرة خطأً
    /// حقيقيًا مؤكدًا من التقرير الأصلي (CQ-01, DB-02) — الهدف منع رجوعها صامتة مستقبلاً.
    ///
    /// ملاحظة: UserManager&lt;IdentityUser&gt; يُمرَّر null! عمدًا — الدوال المُختبَرة هنا
    /// (Create/Update/Delete Section/Question) لا تستخدمه إطلاقًا، فتمرير Mock كامل له كان
    /// سيُضيف تعقيدًا بلا فائدة اختبارية حقيقية.
    /// </summary>
    public class AdminServiceTests
    {
        private static (AdminService service, AppDbContext context) CreateService(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new AppDbContext(options);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new AdminService(context, userManager: null!, cache);
            return (service, context);
        }

        [Fact]
        public async Task UpdateSectionAsync_PreservesOriginalCreatedAt()
        {
            // (CQ-01): كان الكود القديم يستبدل حالة الكيان بالكامل بـ Modified، فيكتب فوق
            // CreatedAt الحقيقي بالقيمة الافتراضية default(DateTime) في كل مرة يُعدَّل فيها القسم.
            var (service, context) = CreateService(nameof(UpdateSectionAsync_PreservesOriginalCreatedAt));

            var originalCreatedAt = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var section = new Section
            {
                SectionNameAr = "قسم تجريبي",
                SectionNameEn = "Test Section",
                Weight = 20,
                DisplayOrder = 1,
                IsActive = true,
                CreatedAt = originalCreatedAt,
                UpdatedAt = originalCreatedAt
            };
            context.Sections.Add(section);
            await context.SaveChangesAsync();

            // محاكاة نموذج تعديل لا يرسل CreatedAt إطلاقًا (تمامًا كما في EditSection.cshtml الفعلية)
            var editedSection = new Section
            {
                Id = section.Id,
                SectionNameAr = "قسم مُعدَّل",
                SectionNameEn = "Edited Section",
                Weight = 25,
                DisplayOrder = 1,
                IsActive = true
                // CreatedAt غير مُسنَدة عمدًا — تبقى default(DateTime)
            };

            await service.UpdateSectionAsync(editedSection);

            var reloaded = await context.Sections.FirstAsync(s => s.Id == section.Id);
            Assert.Equal(originalCreatedAt, reloaded.CreatedAt);
            Assert.Equal("قسم مُعدَّل", reloaded.SectionNameAr);
            Assert.Equal(25, reloaded.Weight);
        }

        [Fact]
        public async Task UpdateQuestionAsync_PreservesOriginalCreatedAt()
        {
            var (service, context) = CreateService(nameof(UpdateQuestionAsync_PreservesOriginalCreatedAt));

            var originalCreatedAt = new DateTime(2025, 2, 1, 10, 0, 0, DateTimeKind.Utc);
            var section = new Section { SectionNameAr = "قسم", SectionNameEn = "Section", Weight = 20, DisplayOrder = 1, IsActive = true };
            context.Sections.Add(section);
            await context.SaveChangesAsync();

            var question = new Question
            {
                SectionId = section.Id,
                QuestionTextAr = "سؤال",
                QuestionTextEn = "Question",
                PointValue = 5,
                DisplayOrder = 1,
                IsActive = true,
                CreatedAt = originalCreatedAt,
                UpdatedAt = originalCreatedAt
            };
            context.Questions.Add(question);
            await context.SaveChangesAsync();

            var edited = new Question
            {
                Id = question.Id,
                SectionId = section.Id,
                QuestionTextAr = "سؤال مُعدَّل",
                QuestionTextEn = "Edited",
                PointValue = 8,
                DisplayOrder = 1,
                IsActive = true
            };

            await service.UpdateQuestionAsync(edited);

            var reloaded = await context.Questions.FirstAsync(q => q.Id == question.Id);
            Assert.Equal(originalCreatedAt, reloaded.CreatedAt);
        }

        [Fact]
        public async Task DeleteSectionAsync_WithNoQuestions_HardDeletes()
        {
            var (service, context) = CreateService(nameof(DeleteSectionAsync_WithNoQuestions_HardDeletes));

            var section = new Section { SectionNameAr = "قسم فارغ", SectionNameEn = "Empty", Weight = 10, DisplayOrder = 1, IsActive = true };
            context.Sections.Add(section);
            await context.SaveChangesAsync();

            var (success, wasSoftDeactivated) = await service.DeleteSectionAsync(section.Id);

            Assert.True(success);
            Assert.False(wasSoftDeactivated);
            Assert.Null(await context.Sections.FindAsync(section.Id));
        }

        [Fact]
        public async Task DeleteSectionAsync_WithExistingQuestions_SoftDeactivatesInsteadOfDeleting()
        {
            // (DB-02): لا حذف فعلي طالما توجد أسئلة مرتبطة — قد تكون جزءًا من قرار قرض معتمد فعليًا.
            var (service, context) = CreateService(nameof(DeleteSectionAsync_WithExistingQuestions_SoftDeactivatesInsteadOfDeleting));

            var section = new Section { SectionNameAr = "قسم فيه أسئلة", SectionNameEn = "Has Questions", Weight = 15, DisplayOrder = 1, IsActive = true };
            context.Sections.Add(section);
            await context.SaveChangesAsync();

            context.Questions.Add(new Question
            {
                SectionId = section.Id,
                QuestionTextAr = "سؤال",
                QuestionTextEn = "Q",
                PointValue = 5,
                DisplayOrder = 1,
                IsActive = true
            });
            await context.SaveChangesAsync();

            var (success, wasSoftDeactivated) = await service.DeleteSectionAsync(section.Id);

            Assert.True(success);
            Assert.True(wasSoftDeactivated);

            var reloaded = await context.Sections.FindAsync(section.Id);
            Assert.NotNull(reloaded); // لم يُحذف فعليًا
            Assert.False(reloaded!.IsActive); // لكنه عُطِّل منطقيًا
        }

        [Fact]
        public async Task DeleteQuestionAsync_WithExistingAnswers_SoftDeactivatesInsteadOfDeleting()
        {
            var (service, context) = CreateService(nameof(DeleteQuestionAsync_WithExistingAnswers_SoftDeactivatesInsteadOfDeleting));

            var section = new Section { SectionNameAr = "قسم", SectionNameEn = "Section", Weight = 10, DisplayOrder = 1, IsActive = true };
            context.Sections.Add(section);
            await context.SaveChangesAsync();

            var question = new Question { SectionId = section.Id, QuestionTextAr = "س", QuestionTextEn = "Q", PointValue = 5, DisplayOrder = 1, IsActive = true };
            context.Questions.Add(question);
            await context.SaveChangesAsync();

            context.ScreeningAnswers.Add(new ScreeningAnswer
            {
                SessionId = 1,
                QuestionId = question.Id,
                Answer = Data.Enums.AnswerType.Yes
            });
            await context.SaveChangesAsync();

            var (success, wasSoftDeactivated) = await service.DeleteQuestionAsync(question.Id);

            Assert.True(success);
            Assert.True(wasSoftDeactivated);
            var reloaded = await context.Questions.FindAsync(question.Id);
            Assert.NotNull(reloaded);
            Assert.False(reloaded!.IsActive);
        }
    }
}
