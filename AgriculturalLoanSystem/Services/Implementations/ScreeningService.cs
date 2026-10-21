using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Infrastructure;
using AgriculturalLoanSystem.Models.ViewModels;
using AgriculturalLoanSystem.Services.Interfaces;
using AgriculturalLoanSystem.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgriculturalLoanSystem.Services.Implementations
{
    public class ScreeningService : IScreeningService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public ScreeningService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<ScreeningDocument?> SaveDocumentAsync(int sessionId, int sectionId, string fileName, string filePath)
        {
            if (!await IsSessionEditableAsync(sessionId)) return null;
            var document = new ScreeningDocument
            {
                SessionId = sessionId,
                SectionId = sectionId,
                FileName = fileName,
                FilePath = filePath,
                UploadedAt = DateTime.UtcNow
            };

            _context.ScreeningDocuments.Add(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<List<ScreeningDocument>> GetSessionDocumentsAsync(int sessionId)
        {
            return await _context.ScreeningDocuments
                .Where(d => d.SessionId == sessionId)
                .Include(d => d.Section).AsNoTracking()
                .ToListAsync();
        }
        public Task<ScreeningDocument?> GetDocumentAsync(int documentId) =>
            _context.ScreeningDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId);

        public async Task<bool> DeleteDocumentAsync(int documentId)
        {
            var document = await _context.ScreeningDocuments.FindAsync(documentId);
            if (document == null || !await IsSessionEditableAsync(document.SessionId)) return false;
            _context.ScreeningDocuments.Remove(document);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<List<Section>> GetSectionsAndQuestionsAsync()
        {
            // (موديل 7 — ج.6): هذه البيانات شبه ثابتة (تتغيّر فقط حين يعدّل الأدمن الأقسام/الأسئلة
            // عبر AdminService، والذي يستدعي CacheKeys.ActiveSectionsWithQuestions من الكاش عند أي
            // تعديل) لكنها تُقرأ في كل تحميل لصفحة أداة الفحص (Tool.cshtml) — أي عشرات المرات لكل
            // جلسة فحص واحدة. التخزين المؤقت يحوّلها من استعلام قاعدة بيانات متكرر إلى قراءة ذاكرة.
            if (_cache.TryGetValue(CacheKeys.ActiveSectionsWithQuestions, out List<Section>? cached) && cached != null)
            {
                return cached;
            }

            // (اكتشاف أثناء كتابة الاختبارات — يكمل DB-02): كان الاستعلام يُضمِّن كل الأسئلة بغض
            // النظر عن IsActive، فسؤال عُطِّل منطقيًا (بدل حذفه فعليًا لوجود إجابات تاريخية مرتبطة
            // به) كان يستمر بالظهور لأي عميل يبدأ جلسة فحص جديدة — يُفشل الهدف الأساسي من التعطيل
            // المنطقي (إخفاؤه عن الجلسات الجديدة مع الحفاظ على السجل التاريخي للجلسات القديمة).
            var sections = await _context.Sections
                .Include(s => s.Questions.Where(q => q.IsActive))
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(CacheKeys.ActiveSectionsWithQuestions, sections, TimeSpan.FromMinutes(15));
            return sections;
        }

        public async Task<int> CreateNewSessionAsync(int clientId, string userId)
        {
            var newSession = new ScreeningSession
            {
                ClientId = clientId,
                ApplicationUserId = userId,
                StartedAt = DateTime.UtcNow,
                SessionStatus = SessionStatus.InProgress,
                // (CQ-06 — إصلاح): كان يُضبط هنا CompletedAt = DateTime.UtcNow رغم أن الجلسة
                // للتو InProgress وليست منتهية — يبقى null حتى الاكتمال الفعلي في
                // ProcessFinalRecommendationAsync أدناه، وإلا فسدت أي استعلامات تفرز حسب
                // "آخر جلسة مكتملة فعليًا" (كما في ClientsController سابقًا).
                UpdatedAt = DateTime.UtcNow
            };

            _context.ScreeningSessions.Add(newSession);
            await _context.SaveChangesAsync();
            return newSession.Id;
        }

        public async Task<ClientDetailsViewModel?> GetClientBriefAsync(int clientId)
        {
            return await _context.Clients
                .Where(c => c.Id == clientId)
                .Select(c => new ClientDetailsViewModel
                {
                    Id = c.Id,
                    FullNameAr = c.FullNameAr,
                    FullNameEn = c.FullNameEn
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<(List<ScreeningAnswer> ExistingAnswers, List<int> CompletedSectionIndexes)> GetSessionProgressAsync(int sessionId, List<Section> sections)
        {
            var existingAnswers = await _context.ScreeningAnswers
                .Where(a => a.SessionId == sessionId)
                .AsNoTracking()
                .ToListAsync();

            var completedIndexes = new List<int>();
            for (int i = 0; i < sections.Count; i++)
            {
                var qIds = sections[i].Questions.Select(q => q.Id).ToList();
                if (qIds.Any() && qIds.All(id => existingAnswers.Any(ea => ea.QuestionId == id)))
                {
                    completedIndexes.Add(i);
                }
            }

            return (existingAnswers, completedIndexes);
        }

        public Task<bool> IsSessionEditableAsync(int sessionId) =>
            _context.ScreeningSessions.AnyAsync(s => s.Id == sessionId &&
                s.SessionStatus == SessionStatus.InProgress && !s.LoanCalculations.Any());

        public async Task SaveSectionAnswersAsync(int sessionId, Dictionary<int, AnswerType> answers)
        {
            if (!await IsSessionEditableAsync(sessionId))
                throw new InvalidOperationException("Completed or linked screenings cannot be edited.");
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var questionIds = answers.Keys;
                var existing = await _context.ScreeningAnswers
                    .Where(a => a.SessionId == sessionId && questionIds.Contains(a.QuestionId))
                    .ToListAsync();

                foreach (var (questionId, answer) in answers)
                {
                    var saved = existing.SingleOrDefault(a => a.QuestionId == questionId);
                    if (saved == null)
                        _context.ScreeningAnswers.Add(new ScreeningAnswer {
                            SessionId = sessionId, QuestionId = questionId, Answer = answer });
                    else
                    {
                        saved.Answer = answer;
                        saved.UpdatedAt = DateTime.UtcNow;
                    }
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> IsSessionCompleteAsync(int sessionId)
        {
            // (نفس الإصلاح أعلاه بـ GetSectionsAndQuestionsAsync): يجب أن يطابق معيار "الاكتمال"
            // فعليًا نفس مجموعة الأسئلة المعروضة بأداة الفحص (نشطة فقط)، وإلا قد تصبح جلسة "غير
            // قابلة للاكتمال أبدًا" إن حُسبت أسئلة معطَّلة ضمن العدد المطلوب دون أن تظهر للمستخدم أصلاً.
            var totalQuestions = await _context.Questions.CountAsync(q => q.Section != null && q.Section.IsActive && q.IsActive);
            var answeredCount = await _context.ScreeningAnswers.CountAsync(a => a.SessionId == sessionId && a.Question.IsActive && a.Question.Section != null && a.Question.Section.IsActive);
            return totalQuestions > 0 && answeredCount == totalQuestions;
        }

        public async Task<RecommendationResultDto> ProcessFinalRecommendationAsync(int sessionId)
        {
            if (!await IsSessionEditableAsync(sessionId) || !await IsSessionCompleteAsync(sessionId))
                throw new InvalidOperationException("Only a complete, in-progress screening can be finalized.");
            var session = await _context.ScreeningSessions
                .Include(s => s.Answers)
                    .ThenInclude(a => a.Question)
                        .ThenInclude(q => q.Section)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null || !session.Answers.Any())
                return new RecommendationResultDto { Status = Recommendation.NotEligible, ReasonAr = "بيانات الجلسة غير مكتملة." };


            decimal finalWeightedScore = 0; int totalSectionsWeight = 0;
            var sectionsGroups = session.Answers.GroupBy(a => a.Question.Section!);

            foreach (var group in sectionsGroups)
            {
                var section = group.Key; var answers = group.ToList();
                int earnedPoints = answers.Where(a => a.Answer == AnswerType.Yes).Sum(a => a.Question.PointValue);

                int totalPossiblePoints = answers.Sum(a => a.Question.PointValue);

                if (totalPossiblePoints > 0)
                {
                    decimal sectionSuccessRatio = (decimal)earnedPoints / totalPossiblePoints;

                    finalWeightedScore += sectionSuccessRatio * section.Weight;

                    totalSectionsWeight += section.Weight;
                }
            }

            decimal scorePercentage = totalSectionsWeight > 0 ? (finalWeightedScore / totalSectionsWeight) * 100 : 0;

            var result = MapScoreToRecommendation(scorePercentage);

            session.Score = scorePercentage;
            session.Recommendation = result.Status;
            session.RecommendationReason = result.ReasonAr;
            session.SessionStatus = SessionStatus.Completed; session.CompletedAt = DateTime.UtcNow; session.TotalYesAnswers = session.Answers.Count(a => a.Answer == AnswerType.Yes);
            session.TotalNoAnswers = session.Answers.Count(a => a.Answer == AnswerType.No);

            await _context.SaveChangesAsync();

            result.FinalScore = scorePercentage;
            return result;
        }

        private RecommendationResultDto MapScoreToRecommendation(decimal score)
        {
            if (score >= 75) return new RecommendationResultDto
            {
                Status = Recommendation.Eligible,
                ReasonAr = $"مؤهل بنتيجة {score:F2}%",
                ReasonEn = $"Eligible with {score:F2}%"
            };

            if (score >= 50) return new RecommendationResultDto
            {
                Status = Recommendation.EligibleWithConditions,
                ReasonAr = $"مؤهل بشروط بنتيجة {score:F2}%",
                ReasonEn = $"Conditional with {score:F2}%"
            };

            return new RecommendationResultDto
            {
                Status = Recommendation.NotEligible,
                ReasonAr = $"غير مؤهل بنتيجة {score:F2}%",
                ReasonEn = $"Not Eligible with {score:F2}%"
            };
        }

        public async Task<EvaluationResultViewModel?> GetEvaluationResultViewModelAsync(int sessionId)
        {
            var session = await _context.ScreeningSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null) return null;

            var answers = await _context.ScreeningAnswers.Where(a => a.SessionId == sessionId).AsNoTracking().ToListAsync();

            return new EvaluationResultViewModel
            {
                SessionId = sessionId,
                ClientId = session.ClientId,
                // (BE-01 — إصلاح): كانت هذه القيمة تُؤخذ من معطى Query String في الرابط مباشرة
                // (?recommendation=Eligible مثلاً) بدل قراءتها من قاعدة البيانات، ما كان يسمح لأي
                // مستخدم بتعديل الرابط يدويًا وعرض توصية مختلفة تمامًا عن التوصية الفعلية المخزَّنة.
                // الآن تُقرأ حصريًا من session.Recommendation المحسوبة والمخزَّنة فعليًا بالخادم.
                Recommendation = session.Recommendation ?? Recommendation.NotEligible,
                YesCount = answers.Count(a => a.Answer == AnswerType.Yes),
                TotalQuestions = answers.Count,
                Score = session.Score
            };
        }

        public async Task<ScreeningSession?> GetSessionReportAsync(int sessionId)
        {
            return await _context.ScreeningSessions
                .Include(s => s.Client)
                .Include(s => s.Documents)
                .Include(s => s.Answers).ThenInclude(a => a.Question).ThenInclude(q => q.Section)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }

        public async Task<bool> HasLinkedLoanAsync(int sessionId)
        {
            return await _context.LoanCalculations.AnyAsync(l => l.SessionId == sessionId);
        }

        // (SEC-03 / موديل 3، بند 3.1): ميثودان مساعدان لفحص ملكية الجلسة/الوثيقة قبل تنفيذ أي
        // إجراء عليها. قرار الصلاحية نفسه (isStaff || isOwner) يبقى في الكونترولر بنفس نمط موديل 2.
        public async Task<int?> GetClientIdForSessionAsync(int sessionId)
        {
            return await _context.ScreeningSessions
                .Where(s => s.Id == sessionId)
                .Select(s => (int?)s.ClientId)
                .FirstOrDefaultAsync();
        }

        public async Task<int?> GetClientIdForDocumentAsync(int documentId)
        {
            return await _context.ScreeningDocuments
                .Where(d => d.Id == documentId)
                .Select(d => (int?)d.Session!.ClientId)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> DeleteSessionAsync(int sessionId)
        {
            var session = await _context.ScreeningSessions.Include(s => s.Answers).FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null) return false;

            if (await HasLinkedLoanAsync(sessionId)) return false;
            _context.ScreeningAnswers.RemoveRange(session.Answers);
            _context.ScreeningSessions.Remove(session);
            return await _context.SaveChangesAsync() > 0;
        }

    }
}