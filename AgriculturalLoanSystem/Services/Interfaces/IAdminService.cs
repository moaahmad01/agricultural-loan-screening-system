using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Models.ViewModels;

namespace AgriculturalLoanSystem.Services.Interfaces
{
    public interface IAdminService
    {
        Task<List<UserManagementViewModel>> GetUsersWithClientDataAsync();

        /// <summary>
        /// (CQ-05 — إصلاح N+1): استعلام واحد يجلب أدوار كل المستخدمين دفعة عبر Join مع
        /// AspNetUserRoles/AspNetRoles، بدل استدعاء FindByIdAsync + GetRolesAsync لكل مستخدم
        /// على حدة داخل حلقة (كان يعني رحلتين إضافيتين لقاعدة البيانات لكل مستخدم في ManageUsers).
        /// </summary>
        Task<Dictionary<string, IList<string>>> GetUserRolesMapAsync();

        Task<List<Section>> GetAllSectionsAsync();
        Task<Section?> GetSectionByIdAsync(int id);
        Task CreateSectionAsync(Section section);
        Task UpdateSectionAsync(Section section);

        /// <summary>
        /// (DB-02): تُعيد (Success, WasSoftDeactivated). لو كان للقسم أسئلة مرتبطة (حتى المعطَّلة
        /// منها)، لا يُحذف فعليًا بل يُعطَّل منطقيًا (IsActive=false) للحفاظ على السجل التاريخي.
        /// </summary>
        Task<(bool Success, bool WasSoftDeactivated)> DeleteSectionAsync(int id);

        Task<List<Question>> GetQuestionsAsync(int? sectionId = null);
        Task<Question?> GetQuestionByIdAsync(int id);
        Task CreateQuestionAsync(Question question);
        Task UpdateQuestionAsync(Question question);

        /// <summary>
        /// (DB-02): تُعيد (Success, WasSoftDeactivated). لو كان للسؤال إجابات فحص مرتبطة، لا يُحذف
        /// فعليًا بل يُعطَّل منطقيًا (IsActive=false) للحفاظ على السجل التاريخي المستخدَم في قرارات
        /// قروض قد تكون مُعتمدة فعليًا.
        /// </summary>
        Task<(bool Success, bool WasSoftDeactivated)> DeleteQuestionAsync(int id);

        Task<(List<LoanCalculation> Items, int Total)> GetPendingLoansAsync(int page = 1, int pageSize = 20);
        Task<bool> ApproveLoanAsync(int loanId);
        Task<bool> RejectLoanAsync(int loanId);

        /// <summary>
        /// (DB-07 — إصلاح): كانت مثبَّتة على آخر 100 سجل فقط بلا أي Pagination أو فلترة، ما يعني
        /// أن سجل التدقيق يفقد فائدته الفعلية للمراجعة بمجرد تجاوز النظام 100 عملية حفظ إجمالاً.
        /// الآن تدعم صفحات + فلترة اختيارية بالتاريخ/المستخدم/اسم الكيان، وتُعيد العدد الإجمالي
        /// المطابق للفلتر لبناء واجهة تصفّح صفحات حقيقية.
        /// </summary>
        Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsAsync(
            int page = 1,
            int pageSize = 50,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? userId = null,
            string? entityName = null);
        Task<Dictionary<string, int>> GetDashboardStatsAsync();
    }
}