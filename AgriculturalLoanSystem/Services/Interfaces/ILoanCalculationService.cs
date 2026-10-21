using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Services.Models;

namespace AgriculturalLoanSystem.Services.Interfaces
{
    public interface ILoanCalculationService
    {
        LoanCalculationResultDto CalculateLoan(LoanInputModel input);

        // (DB-01): كان يقبل string حرًا، أصبح يقبل الـ enum الحقيقي مباشرة.
        decimal GetInterestRate(LoanCategory loanType, decimal amount);

        int GetPaymentsPerYear(PaymentFrequency frequency);
    }
}