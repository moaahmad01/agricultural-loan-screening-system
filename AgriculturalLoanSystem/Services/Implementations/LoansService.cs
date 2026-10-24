using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Services.Interfaces;
using AgriculturalLoanSystem.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriculturalLoanSystem.Services.Implementations
{
    public class LoansService : ILoansService
    {
        private readonly AppDbContext _context;

        public LoansService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> LoanExistsForSessionAsync(int sessionId)
        {
            return await _context.LoanCalculations.AnyAsync(l => l.SessionId == sessionId);
        }

        public async Task<PreviousLoan?> GetActiveOngoingLoanAsync(int clientId)
        {
            return await _context.PreviousLoans
                .Where(pl => pl.ClientId == clientId && pl.Status == LoanStatus.Ongoing)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetClientNameAsync(int clientId)
        {
            return await _context.Clients
                .Where(c => c.Id == clientId)
                .Select(c => c.FullNameAr)
                .FirstOrDefaultAsync();
        }

        public async Task<LoanCalculation> CreateLoanApplicationAsync(
            int clientId,
            int? sessionId,
            LoanCategory loanCategory,
            LoanInputModel input,
            decimal authoritativeAnnualInterestRate,
            LoanCalculationResultDto calculationResult)
        {
            // (FIN-01C — Option A): حارس دفاعي في حدود الحفظ الداخلية — أي نداء لا يمرّ
            // عبر ModelState في MVC يُرفض هنا أيضًا قبل إضافة أي كيان أو SaveChanges،
            // فلا يمكن تخزين أصل قرض غير قابل للتمثيل بدقة منزلتين (يمنع فرق 0.01
            // دينار المخزَّن بين LoanCalculations و PaymentSchedules). الرسالة هي موحّد
            // LoanPrincipalPrecision ورسالتها العربية آمنة للعرض المباشر (CQ-04).
            if (!LoanPrincipalPrecision.IsRepresentableAtTwoDecimals(input.PrincipalAmount))
                throw new ArgumentException(LoanPrincipalPrecision.ErrorMessage);

            if (sessionId.HasValue && !await _context.ScreeningSessions.AnyAsync(
                s => s.Id == sessionId.Value && s.ClientId == clientId))
                throw new ArgumentException("The screening session does not belong to this client.");
            if (sessionId.HasValue && await LoanExistsForSessionAsync(sessionId.Value))
                throw new ArgumentException("A loan already exists for this screening session.");

            var loanEntity = new LoanCalculation
            {
                ClientId = clientId,
                SessionId = sessionId,
                Status = LoanApplicationStatus.PendingApproval,

                PrincipalAmount = input.PrincipalAmount,
                DurationMonths = input.DurationMonths,
                NumberOfPayments = calculationResult.Schedule.Count,
                // (SEC-04): القيمة المحفوظة هنا هي المعدل المُعتمَد من الخادم حصرًا، وليست
                // input.AnnualInterestRate القادمة من نموذج العميل — راجع تعليق الواجهة أعلاه.
                AnnualInterestRate = authoritativeAnnualInterestRate,
                PaymentFrequency = input.PaymentFrequency,
                InterestType = input.InterestType,
                LoanCategory = loanCategory,

                TotalInterest = calculationResult.TotalInterest,
                InstallmentAmount = calculationResult.InstallmentAmount,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                PaymentSchedules = calculationResult.Schedule.Select(s => new PaymentSchedule
                {
                    PaymentNumber = s.PaymentNumber,
                    PaymentDate = s.PaymentDate,
                    PaymentAmount = s.PaymentAmount,
                    PrincipalPaid = s.PrincipalPaid,
                    InterestPaid = s.InterestPaid,
                    RemainingBalance = s.RemainingBalance,
                    CreatedAt = DateTime.UtcNow
                }).ToList()
            };

            _context.LoanCalculations.Add(loanEntity);
            await _context.SaveChangesAsync();

            return loanEntity;
        }

        public async Task<LoanCalculation?> GetApplicationDetailsAsync(int loanId)
        {
            return await _context.LoanCalculations
                .Include(l => l.Client)
                .Include(l => l.PaymentSchedules)
                .FirstOrDefaultAsync(l => l.Id == loanId);
        }
    }
}
