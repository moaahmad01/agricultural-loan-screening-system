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

            // ================================================================
            // (FIN-01B — تنفيذ السياسة النقدية المعتمدة):
            // القيم المعروضة والمحفوظة بدقة منزلتين عشريتين هي المرجع. لكل سطر:
            // مبلغ القسط = المدين + الفائدة، ومجاميع الأسطر تساوي مجاميع الملخص،
            // والرصيد النهائي صفرًا تمامًا، ودون أي مبالغ سالبة.
            // الصيغ المالية والمعدلات ومنطق التواريخ وMidpointRounding.ToEven دون تغيير.
            // ================================================================
            decimal principalAtPrecision = Math.Round(principal, 2, MidpointRounding.ToEven);
            decimal nominalInstallment = Math.Round(paymentAmount, 2, MidpointRounding.ToEven);
            // إجمالي الفائدة التعاقدي مقرّبًا — يُستخدم للفائدة الثابتة فقط.
            decimal totalInterestTarget = Math.Round(totalInterest, 2, MidpointRounding.ToEven);

            var schedule = new List<PaymentScheduleRowDto>();
            decimal remainingBalance = principalAtPrecision;
            decimal accumulatedInterest = 0m;

            for (int i = 1; i <= totalPayments; i++)
            {
                bool isFinal = i == totalPayments;
                decimal interestPaid;

                if (input.InterestType == InterestType.Reducing)
                {
                    // (FIN-01B): فائدة كل فترة تُحسب من الرصيد المُعرَض بدقة منزلتين
                    // (وليس من رصيد غير مقرّب) — وأصل انعدام تطابق الأقساط القديمة
                    // كان احتساب الفائدة والمدين من أرقام غير مقرّبة ثم تقريبها كل على حدة.
                    decimal ratePerPayment = annualRate / paymentsPerYear;
                    interestPaid = Math.Round(remainingBalance * ratePerPayment, 2, MidpointRounding.ToEven);
                }
                else
                {
                    // الفائدة الثابتة: حصة ساوية لكل قسط من إجمالي الفائدة التعاقدي.
                    interestPaid = Math.Round(totalInterest / totalPayments, 2, MidpointRounding.ToEven);
                }

                decimal principalPaid = isFinal ? remainingBalance : nominalInstallment - interestPaid;

                // (FIN-01B): لا يُسدَّد مدين أكبر من الرصيد القائم أبدًا؛ وعند الاقتطاع
                // يُعاد حساب مبلغ هذا القسط من مكوناته (PaymentAmount = مدين + فائدة
                // أدناه) بدل إخفاء أي خلل بـ Math.Max كما في التنفيذ القديم.
                if (principalPaid > remainingBalance)
                {
                    principalPaid = remainingBalance;
                }
                if (principalPaid < 0m)
                {
                    // يصبح سالبًا فقط في الحالات التنكّرية الموثقة (قسط دوري مقرّب
                    // أصغر من فائدة الفترة) — يُصفَّر المدين ويبقى حساب الفائدة كما هو،
                    // ويبقى الرصيد يُسدَّد كاملًا في القسط الأخير.
                    principalPaid = 0m;
                }

                if (isFinal && input.InterestType == InterestType.Simple)
                {
                    // (FIN-01B): للفائدة الثابتة: الفرق المتبقي عند تقريب حصص
                    // الفائدة إلى منزلتين يُخصَّص للقسط الأخير — وكذلك يُخصَّص
                    // له رصيد المدين المتبقي.
                    interestPaid = totalInterestTarget - accumulatedInterest;
                    if (interestPaid < 0m)
                    {
                        // (FIN-01B): حصص الفائدة السابقة (بقرّبها) جمّعت أكثر من
                        // الإجمالي التعاقدي المقرّب — ممكن فقط حين تكون فائدة الفترة
                        // أقل من قرش. نمشي السنتات الزائدة إلى الوراء عبر الأسطر من
                        // الأخير (تبقى فائدة كل سطر ≥ صفرًا) مع إعادة حساب مبلغ كل
                        // سطر من مكوناته، فيبقى مجموع الفائدة = الإجمالي التعاقدي.
                        decimal excess = accumulatedInterest - totalInterestTarget;
                        for (int k = schedule.Count - 1; k >= 0 && excess > 0; k--)
                        {
                            var priorRow = schedule[k];
                            if (priorRow.InterestPaid <= 0m) continue;
                            decimal take = Math.Min(priorRow.InterestPaid, excess);
                            priorRow.InterestPaid -= take;
                            priorRow.PaymentAmount = priorRow.PrincipalPaid + priorRow.InterestPaid;
                            excess -= take;
                            accumulatedInterest -= take;
                        }
                        interestPaid = totalInterestTarget - accumulatedInterest;
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
                    // (FIN-01B): مبلغ القسط يُشتق دائمًا من مكوناته، فيبقى القسط
                    // متسقًا مع المدين والفائدة (خاصة عند اقتطاع المدين أو القسط الأخير).
                    PaymentAmount = principalPaid + interestPaid,
                    PrincipalPaid = principalPaid,
                    InterestPaid = interestPaid,
                    RemainingBalance = remainingBalance
                });
            }

            // (FIN-01B): مجاميع الملخص مشتقة من الجدولة النهائية نفسها — لا يمكن
            // بعد الآن أن يخالف مجموع الأسطر المبلغ المُعلن.
            decimal scheduleInterest = schedule.Sum(r => r.InterestPaid);
            decimal scheduleRepayment = schedule.Sum(r => r.PaymentAmount);

            return new LoanCalculationResultDto
            {
                InstallmentAmount = nominalInstallment,
                TotalInterest = scheduleInterest,
                TotalRepayment = scheduleRepayment,
                Schedule = schedule
            };
        }
    }
}