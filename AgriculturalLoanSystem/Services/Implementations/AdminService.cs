using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Infrastructure;
using AgriculturalLoanSystem.Models.ViewModels;
using AgriculturalLoanSystem.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgriculturalLoanSystem.Services.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IMemoryCache _cache;

        public AdminService(AppDbContext context, UserManager<IdentityUser> userManager, IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _cache = cache;
        }

        public async Task<List<UserManagementViewModel>> GetUsersWithClientDataAsync()
        {
            return await (from user in _userManager.Users
                          join client in _context.Clients on user.Id equals client.ApplicationUserId into userGroup
                          from client in userGroup.DefaultIfEmpty()
                          select new UserManagementViewModel
                          {
                              UserId = user.Id,
                              UserName = user.UserName ?? "---",
                              FullNameAr = client != null ? client.FullNameAr : "---",
                              FullNameEn = client != null ? client.FullNameEn : "---",
                              Region = client != null ? client.Region ?? "---" : "---",
                              PhoneNumber = user.PhoneNumber
                          }).ToListAsync();
        }

        public async Task<Dictionary<string, IList<string>>> GetUserRolesMapAsync()
        {
            // (CQ-05): استعلام واحد بدل حلقة FindByIdAsync/GetRolesAsync لكل مستخدم.
            var pairs = await (from ur in _context.UserRoles
                                join r in _context.Roles on ur.RoleId equals r.Id
                                select new { ur.UserId, RoleName = r.Name }).ToListAsync();

            return pairs
                .GroupBy(p => p.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => (IList<string>)g.Select(p => p.RoleName ?? string.Empty).ToList());
        }

        public async Task<List<Section>> GetAllSectionsAsync()
        {
            return await _context.Sections
   .AsNoTracking()
   .OrderBy(s => s.DisplayOrder)
   .ToListAsync();
        }

        public async Task<Section?> GetSectionByIdAsync(int id)
        {
            return await _context.Sections.FindAsync(id);
        }

        public async Task CreateSectionAsync(Section section)
        {
            // Persist only creation fields, never a caller-supplied navigation graph or identity.
            var entity = new Section
            {
                SectionNameAr = section.SectionNameAr,
                SectionNameEn = section.SectionNameEn,
                Weight = section.Weight,
                DisplayOrder = section.DisplayOrder,
                IsActive = section.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Sections.Add(entity);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.ActiveSectionsWithQuestions);
        }

        public async Task UpdateSectionAsync(Section section)
        {
            // (CQ-01 — إصلاح): كان الكود القديم يضع الكيان بالكامل بحالة Modified، فيعتبر كل
            // خصائصه معدَّلة — بما فيها CreatedAt التي لا يرسلها نموذج التعديل كحقل مخفي، فتصل
            // كـ default(DateTime) وتُكتب فوق تاريخ الإنشاء الحقيقي في كل مرة يُعدَّل فيها القسم.
            // الإصلاح: تحميل الكيان الأصلي من قاعدة البيانات وتحديث الحقول القابلة للتعديل فقط.
            var existing = await _context.Sections.FirstOrDefaultAsync(s => s.Id == section.Id);
            if (existing == null) return;

            existing.SectionNameAr = section.SectionNameAr;
            existing.SectionNameEn = section.SectionNameEn;
            existing.Weight = section.Weight;
            existing.DisplayOrder = section.DisplayOrder;
            existing.IsActive = section.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            // (موديل 7 — ج.6): إبطال إلزامي بعد أي تعديل، وإلا يستمر Tool.cshtml بعرض القسم القديم
            // من الكاش لبقية مدة الصلاحية (حتى 15 دقيقة) لأي مستخدم يبدأ جلسة فحص جديدة.
            _cache.Remove(CacheKeys.ActiveSectionsWithQuestions);
        }


        public async Task<(bool Success, bool WasSoftDeactivated)> DeleteSectionAsync(int id)
        {
            var section = await _context.Sections
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null) return (false, false);

            if (section.Questions.Any())
            {
                // (DB-02): لا حذف فعلي طالما يوجد أسئلة مرتبطة (المرفق الآن بقيد Restrict على
                // مستوى قاعدة البيانات أيضًا) — تعطيل منطقي فقط حفاظًا على السجل التاريخي.
                section.IsActive = false;
                section.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _cache.Remove(CacheKeys.ActiveSectionsWithQuestions);
                return (true, true);
            }

            _context.Sections.Remove(section);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.ActiveSectionsWithQuestions);
            return (true, false);
        }

        public async Task<List<Question>> GetQuestionsAsync(int? sectionId = null)
        {
            var query = _context.Questions.Include(q => q.Section).AsQueryable();

            if (sectionId.HasValue)
            {
                query = query.Where(q => q.SectionId == sectionId);
            }

            return await query
   .OrderBy(q => q.SectionId)
   .ThenBy(q => q.DisplayOrder)
   .ToListAsync();
        }

        public async Task<Question?> GetQuestionByIdAsync(int id)
        {
            return await _context.Questions.Include(q => q.Section).FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task CreateQuestionAsync(Question question)
        {
            if (question.SectionId <= 0 || !await _context.Sections.AnyAsync(s => s.Id == question.SectionId))
                throw new ArgumentException("The selected section does not exist.", nameof(question.SectionId));

            // Use the validated foreign key; the submitted Section object must never be attached.
            var entity = new Question
            {
                SectionId = question.SectionId,
                QuestionTextAr = question.QuestionTextAr,
                QuestionTextEn = question.QuestionTextEn,
                PointValue = question.PointValue,
                DisplayOrder = question.DisplayOrder,
                IsActive = question.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Questions.Add(entity);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.ActiveSectionsWithQuestions);
        }

        public async Task UpdateQuestionAsync(Question question)
        {
            // (CQ-01 — نفس الإصلاح المطبَّق على UpdateSectionAsync أعلاه)
            var existing = await _context.Questions.FirstOrDefaultAsync(q => q.Id == question.Id);
            if (existing == null) return;

            existing.SectionId = question.SectionId;
            existing.QuestionTextAr = question.QuestionTextAr;
            existing.QuestionTextEn = question.QuestionTextEn;
            existing.PointValue = question.PointValue;
            existing.DisplayOrder = question.DisplayOrder;
            existing.IsActive = question.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.ActiveSectionsWithQuestions);
        }

        public async Task<(bool Success, bool WasSoftDeactivated)> DeleteQuestionAsync(int id)
        {
            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == id);
            if (question == null) return (false, false);

            bool hasAnswers = await _context.ScreeningAnswers.AnyAsync(a => a.QuestionId == id);
            if (hasAnswers)
            {
                // (DB-02): إجابات فحص تاريخية قد تكون استُخدمت فعليًا في قرار قرض معتمد — لا حذف فعلي.
                question.IsActive = false;
                question.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _cache.Remove(CacheKeys.ActiveSectionsWithQuestions);
                return (true, true);
            }

            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.ActiveSectionsWithQuestions);
            return (true, false);
        }

        // (DB-07 — موديل 7): إضافة Pagination — قائمة القروض المعلّقة يمكن أن تنمو بمرور الوقت
        // رغم إن الموظفين يعالجونها باستمرار (اعتماد/رفض)، إلا أنها لا تزال قابلة للنمو غير المحدود
        // نظريًا (تراكم طلبات لم تُعالَج). تُعيد أيضًا العدد الإجمالي المطابق لبناء واجهة تصفّح.
        public async Task<(List<LoanCalculation> Items, int Total)> GetPendingLoansAsync(int page = 1, int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.LoanCalculations
                .Include(l => l.Client)
                .Where(l => l.Status == LoanApplicationStatus.PendingApproval);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<bool> ApproveLoanAsync(int loanId)
        {
            var loan = await _context.LoanCalculations.FindAsync(loanId);
            if (loan == null) return false;

            loan.Status = LoanApplicationStatus.Approved;
            loan.UpdatedAt = DateTime.UtcNow;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> RejectLoanAsync(int loanId)
        {
            var loan = await _context.LoanCalculations.FindAsync(loanId);
            if (loan == null) return false;

            loan.Status = LoanApplicationStatus.Rejected;
            loan.UpdatedAt = DateTime.UtcNow;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsAsync(
            int page = 1,
            int pageSize = 50,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? userId = null,
            string? entityName = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var query = _context.AuditLogs.AsQueryable();

            if (fromDate.HasValue) query = query.Where(l => l.Timestamp >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(l => l.Timestamp <= toDate.Value);
            if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(l => l.UserId == userId);
            if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(l => l.EntityName == entityName);

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Dictionary<string, int>> GetDashboardStatsAsync()
        {
            return new Dictionary<string, int>
            {
                { "TotalUsers", await _userManager.Users.CountAsync() },
                { "PendingLoans", await _context.LoanCalculations.CountAsync(l => l.Status == LoanApplicationStatus.PendingApproval) },
                { "ActiveSections", await _context.Sections.CountAsync(s => s.IsActive) },
                { "TotalQuestions", await _context.Questions.CountAsync() }
            };
        }
    }
}
