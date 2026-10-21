using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AgriculturalLoanSystem.Tests.Services
{
    public class ScreeningServiceTests
    {
        private static (ScreeningService service, AppDbContext context) CreateService(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new AppDbContext(options);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new ScreeningService(context, cache);
            return (service, context);
        }

        [Fact]
        public async Task CreateNewSessionAsync_DoesNotSetCompletedAt()
        {
            // (CQ-06): كان الكود القديم يضبط CompletedAt = DateTime.UtcNow عند إنشاء الجلسة رغم
            // أنها InProgress للتو، ما كان يُفسد أي فرز يعتمد على "آخر جلسة مكتملة فعليًا".
            var (service, context) = CreateService(nameof(CreateNewSessionAsync_DoesNotSetCompletedAt));

            var client = new Client { NationalId = "1234567890", FullNameAr = "عميل تجريبي" };
            context.Clients.Add(client);
            await context.SaveChangesAsync();

            var sessionId = await service.CreateNewSessionAsync(client.Id, "test-user-id");

            var session = await context.ScreeningSessions.FirstAsync(s => s.Id == sessionId);
            Assert.Null(session.CompletedAt);
            Assert.Equal(SessionStatus.InProgress, session.SessionStatus);
        }

        [Fact]
        public async Task GetSectionsAndQuestionsAsync_ExcludesDeactivatedQuestions()
        {
            // (اكتشاف أثناء كتابة الاختبارات — يكمل DB-02): سؤال مُعطَّل منطقيًا (IsActive=false)
            // يجب ألا يظهر بجلسات فحص جديدة، حتى لو بقي محفوظًا لأسباب تاريخية.
            var (service, context) = CreateService(nameof(GetSectionsAndQuestionsAsync_ExcludesDeactivatedQuestions));

            var section = new Section { SectionNameAr = "قسم", SectionNameEn = "Section", Weight = 20, DisplayOrder = 1, IsActive = true };
            context.Sections.Add(section);
            await context.SaveChangesAsync();

            context.Questions.AddRange(
                new Question { SectionId = section.Id, QuestionTextAr = "سؤال نشط", QuestionTextEn = "Active", PointValue = 5, DisplayOrder = 1, IsActive = true },
                new Question { SectionId = section.Id, QuestionTextAr = "سؤال معطَّل", QuestionTextEn = "Inactive", PointValue = 5, DisplayOrder = 2, IsActive = false }
            );
            await context.SaveChangesAsync();

            var sections = await service.GetSectionsAndQuestionsAsync();

            var returnedQuestions = sections.Single().Questions;
            Assert.Single(returnedQuestions);
            Assert.True(returnedQuestions.First().IsActive);
        }

        [Fact]
        public async Task IsSessionCompleteAsync_IgnoresDeactivatedQuestionsInTotalCount()
        {
            // يجب أن يطابق معيار "الاكتمال" فعليًا نفس الأسئلة المعروضة (نشطة فقط)، وإلا تصبح
            // الجلسة "غير قابلة للاكتمال أبدًا" بسبب أسئلة معطَّلة لا تظهر للمستخدم أصلاً.
            var (service, context) = CreateService(nameof(IsSessionCompleteAsync_IgnoresDeactivatedQuestionsInTotalCount));

            var client = new Client { NationalId = "1234567891", FullNameAr = "عميل" };
            context.Clients.Add(client);
            var section = new Section { SectionNameAr = "قسم", SectionNameEn = "Section", Weight = 20, DisplayOrder = 1, IsActive = true };
            context.Sections.Add(section);
            await context.SaveChangesAsync();

            var activeQuestion = new Question { SectionId = section.Id, QuestionTextAr = "نشط", QuestionTextEn = "Active", PointValue = 5, DisplayOrder = 1, IsActive = true };
            var inactiveQuestion = new Question { SectionId = section.Id, QuestionTextAr = "معطَّل", QuestionTextEn = "Inactive", PointValue = 5, DisplayOrder = 2, IsActive = false };
            context.Questions.AddRange(activeQuestion, inactiveQuestion);
            await context.SaveChangesAsync();

            var session = new ScreeningSession { ClientId = client.Id, ApplicationUserId = "u1", StartedAt = DateTime.UtcNow, SessionStatus = SessionStatus.InProgress, UpdatedAt = DateTime.UtcNow };
            context.ScreeningSessions.Add(session);
            await context.SaveChangesAsync();

            // أُجيب فقط على السؤال النشط — السؤال المعطَّل لا يجب أن يُحسَب ضمن "الإجمالي المطلوب"
            context.ScreeningAnswers.Add(new ScreeningAnswer { SessionId = session.Id, QuestionId = activeQuestion.Id, Answer = AnswerType.Yes });
            await context.SaveChangesAsync();

            var isComplete = await service.IsSessionCompleteAsync(session.Id);

            Assert.True(isComplete);
        }

        [Fact]
        public async Task GetEvaluationResultViewModelAsync_ReadsRecommendationFromDatabase_NotFromCaller()
        {
            // (BE-01): كانت هذه القيمة تُؤخذ من معطى الرابط (Query String) مباشرة بدل قراءتها من
            // قاعدة البيانات — يعني كان يمكن لأي مستخدم تعديل الرابط لعرض توصية مزيَّفة. التوقيع
            // الجديد لا يقبل recommendation كمعامل إطلاقًا؛ يُقرأ حصريًا من session.Recommendation.
            var (service, context) = CreateService(nameof(GetEvaluationResultViewModelAsync_ReadsRecommendationFromDatabase_NotFromCaller));

            var client = new Client { NationalId = "1234567892", FullNameAr = "عميل" };
            context.Clients.Add(client);
            await context.SaveChangesAsync();

            var session = new ScreeningSession
            {
                ClientId = client.Id,
                ApplicationUserId = "u1",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SessionStatus = SessionStatus.Completed,
                Recommendation = Recommendation.Eligible, // القيمة الحقيقية المخزَّنة بقاعدة البيانات
                Score = 90
            };
            context.ScreeningSessions.Add(session);
            await context.SaveChangesAsync();

            var result = await service.GetEvaluationResultViewModelAsync(session.Id);

            Assert.NotNull(result);
            Assert.Equal(Recommendation.Eligible, result!.Recommendation);
        }
    }
}
