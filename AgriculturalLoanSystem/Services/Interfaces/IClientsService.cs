using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Models.ViewModels;

namespace AgriculturalLoanSystem.Services.Interfaces
{
    /// <summary>
    /// خدمة منطق العمل الخاص بالعملاء (Clients). بدأت في موديل 1 بميثود واحدة، واكتملت هنا في
    /// موديل 2 بسحب كل استعلامات AppDbContext المباشرة التي كانت داخل ClientsController (ARCH-03).
    /// قرار تصميمي مقصود: قرارات التفويض (Authorization / isStaff / isOwner) تبقى داخل الكونترولر
    /// وليس هنا — هذه الخدمة تكتفي بجلب البيانات وتنفيذ عمليات الكتابة، والكونترولر هو من يقرر
    /// "هل يُسمح لهذا المستخدم بهذا الإجراء" لأن هذا هو المكان التقليدي الصحيح لمنطق التفويض في ASP.NET Core.
    /// </summary>
    public interface IClientsService
    {
        Task<Client> LinkOrCreateClientForNewUserAsync(
            string nationalId,
            string fullNameAr,
            string? phoneNumber,
            string applicationUserId);

        Task<Client?> GetClientEntityAsync(int clientId);

        Task<Client?> GetClientByApplicationUserIdAsync(string applicationUserId);

        Task<bool> NationalIdExistsAsync(string nationalId);

        Task<Client> CreateClientAsync(ClientCreateViewModel model);

        /// <summary>
        /// يحدّث الحقول القابلة للتعديل فقط في سجل عميل موجود، ويحفظ مسار مستند الهوية الجديد
        /// إن أُرسل (المسار النهائي يُبنى ويُتحقق منه في الكونترولر عبر FileUploadValidator قبل
        /// استدعاء هذه الدالة — الخدمة هنا تتعامل مع النص النهائي للمسار فقط وليس مع IFormFile).
        /// تُعيد null إن لم يوجد العميل.
        /// </summary>
        Task<Client?> UpdateProfileAsync(
            int id,
            string fullNameAr,
            string? fullNameEn,
            string? phoneNumber,
            DateOnly? dateOfBirth,
            string? region,
            string? detailedArea,
            string? address,
            string? newIdentityDocumentPath);

        // (DB-07 — موديل 7): أُضيفت معاملات page/pageSize لتفعيل Pagination حقيقية بدل إعادة
        // كل النتائج المطابقة دفعة واحدة.
        Task<(List<ClientSearchResult> Items, int Total)> SearchClientsAsync(
            string? query,
            IReadOnlyCollection<string> excludedApplicationUserIds,
            int page = 1,
            int pageSize = 20);

        Task<ClientDetailsViewModel?> GetDetailsAsync(int clientId);

        Task<Dictionary<int, LoanApplicationStatus>> GetLoanStatusMapAsync(int clientId);
    }
}
