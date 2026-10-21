using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Infrastructure;
using AgriculturalLoanSystem.Services.Interfaces;
using AgriculturalLoanSystem.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
namespace AgriculturalLoanSystem.Services.Implementations
{
    public class LoanCalculationService : ILoanCalculationService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public LoanCalculationService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // (DB-01): مطابقة enum مباشرة بدل مطابقة نصية حرة كانت عرضة لعدم التطابق الصامت.
        // (موديل 7 — ج.6): جدول معدلات الفائدة صغير وشبه ثابت (يتغيّر فقط حين يعدّله الأدمن يدويًا
        // مباشرة بقاعدة البيانات — لا توجد حتى الآن واجهة إدارة له بالتطبيق)، ويُقرأ في كل عملية
        // حساب قرض. تخزينه كاملًا بالذاكرة يوفّر رحلة قاعدة بيانات لكل استدعاء.
        public decimal GetInterestRate(LoanCategory loanType, decimal amount)
        {
            var allRates = _cache.GetOrCreate(CacheKeys.InterestRates, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                return _context.InterestRates.AsNoTracking().ToList();
            }) ?? new List<InterestRate>();

            var rateEntity = allRates.FirstOrDefault(r =>
                r.LoanType == loanType &&
                amount >= (r.MinLoanAmount ?? 0) &&
                amount <= (r.MaxLoanAmount ?? decimal.MaxValue));

            if (rateEntity != null)
            {
                return rateEntity.AnnualRate;
            }

            return 9.0m;
        }

        public int GetPaymentsPerYear(PaymentFrequency frequency)
        {
            return frequency switch
            {
                PaymentFrequency.Weekly => 52,
                PaymentFrequency.Biweekly => 26,
                PaymentFrequency.Monthly => 12,
                PaymentFrequency.Bimonthly => 6,
                PaymentFrequency.Quarterly => 4,
                PaymentFrequency.Semiannual => 2,
                PaymentFrequency.Annual => 1,
                _ => 12
            };
        }

        public LoanCalculationResultDto CalculateLoan(LoanInputModel input)
        {
            decimal principal = input.PrincipalAmount;
            decimal annualRate = input.AnnualInterestRate / 100m; int totalMonths = input.DurationMonths;

            int paymentsPerYear = GetPaymentsPerYear(input.PaymentFrequency);

            int totalPayments = (int)Math.Ceiling((double)totalMonths / 12 * paymentsPerYear);

            // (CQ-04 — إصلاح): كان بالإمكان الوصول لهذه النقطة بـ totalPayments = 0 (مثلاً
            // DurationMonths = 0) بلا أي فحص، ما يُسبِّب DivideByZeroException غير معالَج لاحقًا
            // عند القسمة على totalPayments. [Range] المُضافة على LoanInputModel تمنع هذا من واجهة
            // المستخدم، لكن هذا الفحص الدفاعي يبقى ضروريًا لأن الخدمة قد تُستدعى من أي مكان آخر
            // مستقبلاً دون المرور بالضرورة عبر تحقق الـ ViewModel.
            if (totalPayments <= 0)
            {
                throw new ArgumentException(
                    "مدة القرض وتكرار السداد المُدخلان ينتجان صفر دفعات — يرجى إدخال مدة أطول أو تكرار سداد مختلف.",
                    nameof(input.DurationMonths));
            }

            decimal paymentAmount = 0m;
            decimal totalInterest = 0m;

            if (input.InterestType == InterestType.Reducing)
            {
                if (annualRate == 0)
                {
                    paymentAmount = principal / totalPayments;
                }
                else
                {
                    decimal ratePerPayment = annualRate / paymentsPerYear;
                    decimal factor = (decimal)Math.Pow((double)(1 + ratePerPayment), totalPayments);
                    paymentAmount = principal * ratePerPayment * factor / (factor - 1);
                }
            }
            else
            {
                totalInterest = principal * annualRate * (totalMonths / 12m);
                decimal totalRepayment = principal + totalInterest;
                paymentAmount = totalRepayment / totalPayments;
            }

            var schedule = new List<PaymentScheduleRowDto>();
            decimal remainingBalance = principal;
            decimal accumulatedInterest = 0m;

            for (int i = 1; i <= totalPayments; i++)
            {
                decimal interestPaid = 0m;
                decimal principalPaid = 0m;

                if (input.InterestType == InterestType.Reducing)
                {
                    decimal ratePerPayment = annualRate / paymentsPerYear;
                    interestPaid = remainingBalance * ratePerPayment;
                    principalPaid = paymentAmount - interestPaid;
                }
                else
                {
                    interestPaid = totalInterest / totalPayments;
                    principalPaid = paymentAmount - interestPaid;
                }

                if (i == totalPayments)
                {
                    if (input.InterestType == InterestType.Reducing)
                    {
                        principalPaid = remainingBalance;
                        paymentAmount = principalPaid + interestPaid;
                    }
                }

                remainingBalance -= principalPaid;
                accumulatedInterest += interestPaid;

                schedule.Add(new PaymentScheduleRowDto
                {
                    PaymentNumber = i,
                    PaymentDate = input.PaymentFrequency switch
                    {
                        PaymentFrequency.Weekly => DateTime.Today.AddDays(i * 7),
                        PaymentFrequency.Biweekly => DateTime.Today.AddDays(i * 14),
                        _ => DateTime.Today.AddMonths(i * (12 / paymentsPerYear))
                    },
                    PaymentAmount = Math.Round(paymentAmount, 2),
                    PrincipalPaid = Math.Round(principalPaid, 2),
                    InterestPaid = Math.Round(interestPaid, 2),
                    RemainingBalance = Math.Max(0m, Math.Round(remainingBalance, 2))
                });
            }

            return new LoanCalculationResultDto
            {
                InstallmentAmount = Math.Round(paymentAmount, 2),
                TotalInterest = Math.Round(accumulatedInterest, 2),
                TotalRepayment = Math.Round(principal + accumulatedInterest, 2),
                Schedule = schedule
            };
        }
    }
}