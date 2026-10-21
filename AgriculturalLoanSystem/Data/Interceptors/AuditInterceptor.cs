using AgriculturalLoanSystem.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace AgriculturalLoanSystem.Data.Interceptors
{
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        // (CQ-07 جزء 1 — إصلاح): لم يكن هذا الـ Interceptor يعرف هوية المستخدم الفاعل إطلاقًا
        // (لا يوجد وصول لـ HttpContext)، فكانت حقول AuditLog.UserId/UserName تبقى فارغة دومًا رغم
        // وجودهما بالكيان — ما يُفرغ سجل التدقيق من أهم قيمة له في نظام مالي: "من فعل ماذا".
        // الحقن هنا آمن رغم أن AuditInterceptor مسجَّلة Singleton، لأن IHttpContextAccessor نفسها
        // مصمَّمة لتكون Singleton-safe (تعتمد AsyncLocal داخليًا).
        public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            AddAuditLogs(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            AddAuditLogs(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void AddAuditLogs(DbContext? context)
        {
            if (context == null) return;

            var httpContext = _httpContextAccessor.HttpContext;
            var userId = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            var entries = context.ChangeTracker.Entries()
   .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
   .ToList();

            foreach (var entry in entries)
            {
                if (entry.Entity is AuditLog) continue;

                var auditLog = new AuditLog
                {
                    EntityName = entry.Entity.GetType().Name,
                    Action = entry.State.ToString(),
                    Timestamp = DateTime.UtcNow,
                    EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
                    UserId = userId,
                    UserName = null,
                    // (CQ-07 جزء 2 — إصلاح): الفلترة القديمة `.Where(p => p.IsModified)` لا تلتقط
                    // شيئًا مفيدًا لحالة Deleted (لا "تعديل" بالمعنى المتعارف عليه، بل إزالة كاملة)،
                    // فكان السجل يُسجَّل بـ Action=Deleted بلا أي تفاصيل عمّا حُذف فعليًا. الآن كل
                    // حالة تُعامَل بمنطقها الصحيح: Added تلتقط القيم الجديدة، Deleted تلتقط القيم
                    // الأصلية قبل الحذف، وModified تبقى كما كانت (قبل/بعد للحقول المتغيرة فقط).
                    Changes = JsonSerializer.Serialize(BuildChanges(entry))
                };

                context.Set<AuditLog>().Add(auditLog);
            }
        }

        private static List<ChangeEntry> BuildChanges(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            // Values are intentionally omitted: Identity hashes, tokens and client PII
            // must never be duplicated into an audit table. Keep actor, entity and field names.
            return entry.Properties
                .Where(p => entry.State != EntityState.Modified || p.IsModified)
                .Select(p => new ChangeEntry(p.Metadata.Name, "[redacted]", "[redacted]"))
                .ToList();
        }

        // نوع مسمّى بدل الاعتماد على أنواع مجهولة (Anonymous Types) عبر فروع switch مختلفة —
        // أوضح للقراءة ويضمن نوعًا موحّدًا واحدًا بلا أي التباس بالاستدلال. أسماء الخصائص هنا
        // (Name/OriginalValue/CurrentValue) مطابقة عمدًا لما تتوقعه Views/Admin/AuditLogs.cshtml
        // عند قراءة JSON المُخزَّن (change.GetProperty("Name") إلخ).
        private record ChangeEntry(string Name, object? OriginalValue, object? CurrentValue);
    }
}
