using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Services.Models;

namespace AgriculturalLoanSystem.Services.Interfaces
{
    /// <summary>
    /// خدمة منطق العمل الخاص بطلبات القروض. أُنشئت في موديل 4 لسحب منطق بناء كيان
    /// LoanCalculation والاستعلامات المباشرة التي كانت داخل LoansController (ARCH-03)،
    /// بنفس نمط IClientsService/IScreeningService من الموديلات السابقة.
    /// </summary>
    public interface ILoansService
    {
        Task<bool> LoanExistsForSessionAsync(int sessionId);

        Task<PreviousLoan?> GetActiveOngoingLoanAsync(int clientId);

        Task<string?> GetClientNameAsync(int clientId);

        /// <summary>
        /// ينشئ طلب القرض ويحفظه. لا يقبل معدل الفائدة من الطلب إطلاقًا — المعدل المُعتمَد يجب
        /// أن يكون قد حُسب مسبقًا في الكونترولر عبر ILoanCalculationService.GetInterestRate
        /// (SEC-04) ويُمرَّر هنا صراحة كمعامل منفصل يوضّح أنه القيمة الموثوقة الوحيدة.
        /// </summary>
        Task<LoanCalculation> CreateLoanApplicationAsync(
            int clientId,
            int? sessionId,
            LoanCategory loanCategory,
            LoanInputModel input,
            decimal authoritativeAnnualInterestRate,
            LoanCalculationResultDto calculationResult);

        Task<LoanCalculation?> GetApplicationDetailsAsync(int loanId);
    }
}
