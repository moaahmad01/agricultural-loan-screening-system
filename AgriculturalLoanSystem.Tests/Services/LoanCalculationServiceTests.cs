using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Services.Implementations;
using AgriculturalLoanSystem.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AgriculturalLoanSystem.Tests.Services
{
    /// <summary>
    /// اختبارات وحدة لـ LoanCalculationService (موديل 7 — ج.4، أولوية 1): أهم منطق مالي حسّاس
    /// بكامل المشروع، ورُصد فيه Edge Case حقيقي (CQ-04) أثناء المراجعة الأصلية.
    /// تستخدم EF Core InMemory بدل SQL Server حقيقي — كافٍ هنا لأن الدالة المُختبَرة الأساسية
    /// (CalculateLoan) لا تلمس قاعدة البيانات إطلاقًا؛ GetInterestRate هي الوحيدة التي تحتاجها.
    /// </summary>
    public class LoanCalculationServiceTests
    {
        private static LoanCalculationService CreateService(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new AppDbContext(options);
            var cache = new MemoryCache(new MemoryCacheOptions());
            return new LoanCalculationService(context, cache);
        }

        [Theory]
        [InlineData(PaymentFrequency.Weekly, 7)]
        [InlineData(PaymentFrequency.Biweekly, 14)]
        public void WeeklySchedulesAdvanceByCalendarDays(PaymentFrequency frequency, int days)
        {
            var service = CreateService(Guid.NewGuid().ToString());
            var today = DateTime.Today;
            var result = service.CalculateLoan(new LoanInputModel {
                PrincipalAmount = 1000, AnnualInterestRate = 6.5m, DurationMonths = 12,
                PaymentFrequency = frequency, InterestType = InterestType.Simple });
            Assert.Equal(today.AddDays(days), result.Schedule[0].PaymentDate);
            Assert.Equal(today.AddDays(2 * days), result.Schedule[1].PaymentDate);
        }

        [Fact]
        public void CalculateLoan_WithZeroDurationMonths_ThrowsArgumentException()
        {
            // (CQ-04): كان هذا السيناريو يُسبِّب DivideByZeroException غامضة قبل الإصلاح.
            var service = CreateService(nameof(CalculateLoan_WithZeroDurationMonths_ThrowsArgumentException));
            var input = new LoanInputModel
            {
                PrincipalAmount = 10000,
                AnnualInterestRate = 7,
                DurationMonths = 0,
                PaymentFrequency = PaymentFrequency.Monthly,
                InterestType = InterestType.Simple
            };

            Assert.Throws<ArgumentException>(() => service.CalculateLoan(input));
        }

        [Theory]
        [InlineData(PaymentFrequency.Monthly, 12, 12)]
        [InlineData(PaymentFrequency.Quarterly, 12, 4)]
        [InlineData(PaymentFrequency.Annual, 24, 2)]
        [InlineData(PaymentFrequency.Weekly, 12, 52)]
        public void CalculateLoan_ScheduleLength_MatchesExpectedPaymentCount(
            PaymentFrequency frequency, int durationMonths, int expectedPayments)
        {
            var service = CreateService(Guid.NewGuid().ToString());
            var input = new LoanInputModel
            {
                PrincipalAmount = 10000,
                AnnualInterestRate = 7,
                DurationMonths = durationMonths,
                PaymentFrequency = frequency,
                InterestType = InterestType.Simple
            };

            var result = service.CalculateLoan(input);

            Assert.Equal(expectedPayments, result.Schedule.Count);
        }

        [Fact]
        public void CalculateLoan_SimpleInterest_ZeroRate_HasZeroTotalInterest()
        {
            var service = CreateService(Guid.NewGuid().ToString());
            var input = new LoanInputModel
            {
                PrincipalAmount = 12000,
                AnnualInterestRate = 0,
                DurationMonths = 12,
                PaymentFrequency = PaymentFrequency.Monthly,
                InterestType = InterestType.Simple
            };

            var result = service.CalculateLoan(input);

            Assert.Equal(0m, result.TotalInterest);
            Assert.Equal(1000m, result.InstallmentAmount); // 12000 / 12 دفعات بلا فائدة
        }

        [Fact]
        public void CalculateLoan_ReducingInterest_LastPayment_FullyClearsRemainingBalance()
        {
            // اختبار سلامة الجدول: القسط الأخير يجب أن يُصفّر الرصيد المتبقي تمامًا (بلا فروقات
            // تراكمية ناتجة عن التقريب عبر عدة دفعات)، وإلا فهذه علامة خطأ حسابي حقيقي.
            var service = CreateService(Guid.NewGuid().ToString());
            var input = new LoanInputModel
            {
                PrincipalAmount = 10000,
                AnnualInterestRate = 8.5m,
                DurationMonths = 24,
                PaymentFrequency = PaymentFrequency.Monthly,
                InterestType = InterestType.Reducing
            };

            var result = service.CalculateLoan(input);

            Assert.Equal(0m, result.Schedule.Last().RemainingBalance);
        }

        [Fact]
        public void CalculateLoan_SimpleInterest_PositiveRate_MatchesManualFormula()
        {
            // principal * rate * (months/12) — التحقق من مطابقة الصيغة اليدوية المباشرة.
            var service = CreateService(Guid.NewGuid().ToString());
            var input = new LoanInputModel
            {
                PrincipalAmount = 10000,
                AnnualInterestRate = 10, // 10%
                DurationMonths = 12,
                PaymentFrequency = PaymentFrequency.Monthly,
                InterestType = InterestType.Simple
            };

            var result = service.CalculateLoan(input);

            // 10000 * 0.10 * (12/12) = 1000
            Assert.Equal(1000m, result.TotalInterest);
        }

        [Theory]
        [InlineData(PaymentFrequency.Weekly, 52)]
        [InlineData(PaymentFrequency.Biweekly, 26)]
        [InlineData(PaymentFrequency.Monthly, 12)]
        [InlineData(PaymentFrequency.Bimonthly, 6)]
        [InlineData(PaymentFrequency.Quarterly, 4)]
        [InlineData(PaymentFrequency.Semiannual, 2)]
        [InlineData(PaymentFrequency.Annual, 1)]
        public void GetPaymentsPerYear_ReturnsCorrectMapping(PaymentFrequency frequency, int expected)
        {
            var service = CreateService(Guid.NewGuid().ToString());
            Assert.Equal(expected, service.GetPaymentsPerYear(frequency));
        }

        // =====================================================================
        // FIN-01B — repayment rounding policy regression tests.
        // Policy: two-decimal monetary values are authoritative; every displayed
        // (and persisted) amount must reconcile: pay = principal + interest per
        // row, component sums equal the reported summaries, no negative amounts,
        // final remaining balance exactly zero. Existing formulas, frequencies,
        // date logic and MidpointRounding.ToEven behavior are unchanged.
        // =====================================================================

        private static void AssertScheduleInvariants(LoanCalculationResultDto result,
            decimal principalAtMonetaryPrecision, decimal? expectedNominalInstallment = null)
        {
            var schedule = result.Schedule;
            Assert.NotEmpty(schedule);

            decimal sumPay = 0m, sumPrincipal = 0m, sumInterest = 0m;
            foreach (var row in schedule)
            {
                Assert.Equal(row.PaymentAmount, row.PrincipalPaid + row.InterestPaid);
                Assert.True(row.PrincipalPaid >= 0m, "principal must never be negative");
                Assert.True(row.InterestPaid >= 0m, "interest must never be negative");
                Assert.True(row.PaymentAmount >= 0m);
                Assert.True(row.RemainingBalance >= 0m);

                Assert.Equal(0m, row.PaymentAmount % 0.01m);
                Assert.Equal(0m, row.PrincipalPaid % 0.01m);
                Assert.Equal(0m, row.InterestPaid % 0.01m);
                Assert.Equal(0m, row.RemainingBalance % 0.01m);

                sumPay += row.PaymentAmount;
                sumPrincipal += row.PrincipalPaid;
                sumInterest += row.InterestPaid;
            }

            Assert.Equal(principalAtMonetaryPrecision, sumPrincipal);
            Assert.Equal(result.TotalInterest, sumInterest);
            Assert.Equal(result.TotalRepayment, sumPay);
            Assert.Equal(0m, schedule.Last().RemainingBalance);

            // InstallmentAmount is the nominal regular installment, not a mutated
            // final installment. When the caller supplies an independently
            // established nominal value (exact test cases), assert against it;
            // otherwise fall back to the derived comparison with the first row,
            // which holds for every scenario without an early cap or walk-back
            // (invariant sweeps). Walk-back scenarios supply an explicit value
            // because their last rows fall below the nominal by design.
            if (schedule.Count > 1)
            {
                if (expectedNominalInstallment is { } nominal)
                {
                    Assert.Equal(nominal, result.InstallmentAmount);
                }
                else
                {
                    Assert.Equal(schedule.First().PaymentAmount, result.InstallmentAmount);
                }
            }
        }

        private static void AssertComponentSums(LoanCalculationResultDto result,
            decimal expectedSumPay, decimal expectedSumPrincipal, decimal expectedSumInterest)
        {
            Assert.Equal(expectedSumPay, result.Schedule.Sum(r => r.PaymentAmount));
            Assert.Equal(expectedSumPrincipal, result.Schedule.Sum(r => r.PrincipalPaid));
            Assert.Equal(expectedSumInterest, result.Schedule.Sum(r => r.InterestPaid));
        }

        [Fact]
        public void CalculateLoan_SimpleInterest_FiveRows_1000At6p5Percent_ReconcilesExactly()
        {
            // Reproduced FIN-01A case. Old behavior: row sum 1027.10 vs reported 1027.08.
            var service = CreateService(nameof(CalculateLoan_SimpleInterest_FiveRows_1000At6p5Percent_ReconcilesExactly));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1000, AnnualInterestRate = 6.5m, DurationMonths = 5,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            });

            var expected = new Dictionary<int, (decimal Pay, decimal P, decimal I, decimal Bal)>
            {
                [1] = (205.42m, 200.00m, 5.42m, 800.00m),
                [2] = (205.42m, 200.00m, 5.42m, 600.00m),
                [3] = (205.42m, 200.00m, 5.42m, 400.00m),
                [4] = (205.42m, 200.00m, 5.42m, 200.00m),
                [5] = (205.40m, 200.00m, 5.40m, 0.00m),
            };
            foreach (var (number, (pay, p, i, bal)) in expected)
            {
                var row = result.Schedule.Single(r => r.PaymentNumber == number);
                Assert.Equal(pay, row.PaymentAmount);
                Assert.Equal(p, row.PrincipalPaid);
                Assert.Equal(i, row.InterestPaid);
                Assert.Equal(bal, row.RemainingBalance);
            }

            Assert.Equal(205.42m, result.InstallmentAmount);
            Assert.Equal(27.08m, result.TotalInterest);
            Assert.Equal(1027.08m, result.TotalRepayment);
            AssertScheduleInvariants(result, 1000m, 205.42m);
        }

        [Fact]
        public void CalculateLoan_ReducingInterest_FiveRows_1000At6p5Percent_ReconcilesExactly()
        {
            // Reproduced FIN-01A case. Old behavior: three rows violated
            // pay = principal + interest, principal rows summed to 1000.01 and row
            // payment sum was 1016.30 vs reported 1016.31.
            var service = CreateService(nameof(CalculateLoan_ReducingInterest_FiveRows_1000At6p5Percent_ReconcilesExactly));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1000, AnnualInterestRate = 6.5m, DurationMonths = 5,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Reducing
            });

            var expected = new Dictionary<int, (decimal Pay, decimal P, decimal I, decimal Bal)>
            {
                [1] = (203.26m, 197.84m, 5.42m, 802.16m),
                [2] = (203.26m, 198.91m, 4.35m, 603.25m),
                [3] = (203.26m, 199.99m, 3.27m, 403.26m),
                [4] = (203.26m, 201.08m, 2.18m, 202.18m),
                [5] = (203.28m, 202.18m, 1.10m, 0.00m),
            };
            foreach (var (number, (pay, p, i, bal)) in expected)
            {
                var row = result.Schedule.Single(r => r.PaymentNumber == number);
                Assert.Equal(pay, row.PaymentAmount);
                Assert.Equal(p, row.PrincipalPaid);
                Assert.Equal(i, row.InterestPaid);
                Assert.Equal(bal, row.RemainingBalance);
            }

            Assert.Equal(203.26m, result.InstallmentAmount);   // nominal regular installment
            Assert.Equal(16.32m, result.TotalInterest);        // derived from the final schedule
            Assert.Equal(1016.32m, result.TotalRepayment);     // derived from the final schedule
            AssertScheduleInvariants(result, 1000m, 203.26m);
        }

        [Theory]
        [InlineData(InterestType.Simple)]
        [InlineData(InterestType.Reducing)]
        public void CalculateLoan_ZeroInterest_PaysExactlyThePrincipal(InterestType interestType)
        {
            var service = CreateService($"{nameof(CalculateLoan_ZeroInterest_PaysExactlyThePrincipal)}_{interestType}");
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 12000, AnnualInterestRate = 0, DurationMonths = 12,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = interestType
            });

            Assert.Equal(0m, result.TotalInterest);
            Assert.Equal(12000m, result.TotalRepayment);
            Assert.Equal(1000m, result.InstallmentAmount);
            Assert.All(result.Schedule, r =>
            {
                Assert.Equal(0m, r.InterestPaid);
                Assert.Equal(1000m, r.PaymentAmount);
                Assert.Equal(1000m, r.PrincipalPaid);
            });
            Assert.Equal(0m, result.Schedule.Last().RemainingBalance);
            AssertScheduleInvariants(result, 12000m, 1000.00m);
        }

        [Theory]
        [InlineData(InterestType.Simple)]
        [InlineData(InterestType.Reducing)]
        public void CalculateLoan_FractionalInstallments_10000At7Percent_12Months_Reconciles(InterestType interestType)
        {
            var service = CreateService($"{nameof(CalculateLoan_FractionalInstallments_10000At7Percent_12Months_Reconciles)}_{interestType}");
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 10000, AnnualInterestRate = 7, DurationMonths = 12,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = interestType
            });
            // NOMINAL installments established independently of the implementation:
            // simple = round((10000 + 700.00)/12) = round(891.6666...) = 891.67;
            // reducing = standard annuity payment round(10000 * (0.07/12) * f/(f-1))
            // with f = (1 + 0.07/12)^12 = 1.07229008... = 865.2675 -> 865.27 (ToEven).
            decimal expectedNominal = interestType == InterestType.Simple ? 891.67m : 865.27m;
            Assert.Equal(expectedNominal, result.InstallmentAmount);
            AssertScheduleInvariants(result, 10000m, expectedNominal);
        }

        [Fact]
        public void CalculateLoan_FractionalSimple_SplitsResidualCentsIntoFinalInstallment()
        {
            // 10000 * 7% * 1 year = 700.00 total interest.
            // Nominal installment = round(10700/12) = 891.67;
            // each row earns 58.33 and pays 833.34 of principal.
            // Rows 1..11 leave 833.26 of principal and 58.37 of residual interest
            // for the final installment.
            var service = CreateService(nameof(CalculateLoan_FractionalSimple_SplitsResidualCentsIntoFinalInstallment));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 10000, AnnualInterestRate = 7, DurationMonths = 12,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            });

            Assert.Equal(891.67m, result.InstallmentAmount);
            Assert.Equal(700.00m, result.TotalInterest);
            Assert.Equal(10700.00m, result.TotalRepayment);

            var first = result.Schedule.First();
            Assert.Equal(891.67m, first.PaymentAmount);
            Assert.Equal(833.34m, first.PrincipalPaid);
            Assert.Equal(58.33m, first.InterestPaid);

            var last = result.Schedule.Last();
            Assert.Equal(833.26m, last.PrincipalPaid);   // 10000 - 11*833.34
            Assert.Equal(58.37m, last.InterestPaid);     // 700.00 - 11*58.33
            Assert.Equal(891.63m, last.PaymentAmount);   // principal + interest
            Assert.Equal(0m, last.RemainingBalance);
            AssertComponentSums(result, 10700.00m, 10000.00m, 700.00m);
            AssertScheduleInvariants(result, 10000m, 891.67m);
        }

        [Theory]
        [InlineData(PaymentFrequency.Weekly, InterestType.Simple)]
        [InlineData(PaymentFrequency.Weekly, InterestType.Reducing)]
        [InlineData(PaymentFrequency.Biweekly, InterestType.Simple)]
        [InlineData(PaymentFrequency.Biweekly, InterestType.Reducing)]
        [InlineData(PaymentFrequency.Monthly, InterestType.Simple)]
        [InlineData(PaymentFrequency.Monthly, InterestType.Reducing)]
        [InlineData(PaymentFrequency.Bimonthly, InterestType.Simple)]
        [InlineData(PaymentFrequency.Bimonthly, InterestType.Reducing)]
        [InlineData(PaymentFrequency.Quarterly, InterestType.Simple)]
        [InlineData(PaymentFrequency.Quarterly, InterestType.Reducing)]
        [InlineData(PaymentFrequency.Semiannual, InterestType.Simple)]
        [InlineData(PaymentFrequency.Semiannual, InterestType.Reducing)]
        [InlineData(PaymentFrequency.Annual, InterestType.Simple)]
        [InlineData(PaymentFrequency.Annual, InterestType.Reducing)]
        public void CalculateLoan_AllFrequencies_KeepScheduleInvariants_24Months(PaymentFrequency frequency, InterestType interestType)
        {
            var service = CreateService($"{nameof(CalculateLoan_AllFrequencies_KeepScheduleInvariants_24Months)}_{frequency}_{interestType}");
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1000, AnnualInterestRate = 9, DurationMonths = 24,
                PaymentFrequency = frequency, InterestType = interestType
            });
            AssertScheduleInvariants(result, 1000m);
        }

        [Fact]
        public void CalculateLoan_SinglePayment_ChargesFullPeriodInterest()
        {
            // One payment carries the whole principal and one full period of interest.
            var service = CreateService(nameof(CalculateLoan_SinglePayment_ChargesFullPeriodInterest));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1000, AnnualInterestRate = 12, DurationMonths = 12,
                PaymentFrequency = PaymentFrequency.Annual, InterestType = InterestType.Reducing
            });

            var only = result.Schedule.Single();
            Assert.Equal(1000m, only.PrincipalPaid);
            Assert.Equal(120m, only.InterestPaid);            // 1210-1008? formula: 1000*0.12
            Assert.Equal(0.00m, only.RemainingBalance);
            Assert.Equal(result.TotalRepayment, only.PaymentAmount);
            // Nominal installment established independently: round(1000 * 0.12 + 1000) = 1120.00.
            Assert.Equal(1120.00m, result.InstallmentAmount);
            AssertScheduleInvariants(result, 1000m, 1120.00m);
        }

        [Fact]
        public void CalculateLoan_LongReducingAmortization_96MonthsAt33Percent_StaysConsistent()
        {
            var service = CreateService(nameof(CalculateLoan_LongReducingAmortization_96MonthsAt33Percent_StaysConsistent));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1000, AnnualInterestRate = 33, DurationMonths = 96,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Reducing
            });

            Assert.Equal(96, result.Schedule.Count);
            AssertScheduleInvariants(result, 1000m);
            // every regular (non-final) payment equals the nominal installment
            Assert.All(result.Schedule.Take(95), r => Assert.Equal(result.InstallmentAmount, r.PaymentAmount));
        }

        [Fact]
        public void CalculateLoan_SmallPrincipalLowRate_StaysConsistent()
        {
            var service = CreateService(nameof(CalculateLoan_SmallPrincipalLowRate_StaysConsistent));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 100, AnnualInterestRate = 9, DurationMonths = 24,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            });
            Assert.Equal(18.00m, result.TotalInterest);       // 100 * 9% * 2 years
            AssertScheduleInvariants(result, 100m);
        }

        [Fact]
        public void CalculateLoan_ExtremeInput_OneDinarOver600Months_KeepsMonetaryInvariants()
        {
            // Documented degenerate case (FIN-01A/01B-R owner notice, NOT an approved
            // rule): 1 JOD over 600 months at 9% produces a 0.01 nominal installment
            // equal to the rounded per-period interest round(1.00 * 0.09/12 = 0.0075)
            // = 0.01, so NONE of the first 599 rows amortizes principal: every regular
            // row pays 0.01 (0.00 principal + 0.01 interest) on a constant 1.00
            // balance, and the final row repays the entire principal (1.00) plus 0.01
            // interest = 1.01 (a balloon). There is no premature payoff and no
            // zero-value row; reducing-balance total interest is 0.01 x 600 = 6.00
            // versus 4.50 for simple interest (see the companion test below). Rules
            // 1-8 still hold numerically; a minimum-installment validation would be a
            // separate owner decision.
            var service = CreateService(nameof(CalculateLoan_ExtremeInput_OneDinarOver600Months_KeepsMonetaryInvariants));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1, AnnualInterestRate = 9, DurationMonths = 600,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Reducing
            });

            Assert.Equal(600, result.Schedule.Count);
            Assert.Equal(0.01m, result.InstallmentAmount);
            Assert.Equal(6.00m, result.TotalInterest);
            Assert.Equal(7.00m, result.TotalRepayment);
            Assert.Equal(599, result.Schedule.Take(599).Count(r =>
                r.PaymentAmount == 0.01m && r.PrincipalPaid == 0.00m && r.InterestPaid == 0.01m));
            var final = result.Schedule.Last();
            Assert.Equal(1.01m, final.PaymentAmount);
            Assert.Equal(1.00m, final.PrincipalPaid);
            Assert.Equal(0.01m, final.InterestPaid);
            Assert.Equal(0m, final.RemainingBalance);
            AssertScheduleInvariants(result, 1.00m, 0.01m);
        }

        [Fact]
        public void CalculateLoan_DegenerateSimpleInterest_OneDinarAt9Percent_600Months_WalkBackBalloon()
        {
            // Documented degenerate case (FIN-01A/01B-R owner notice, NOT an approved
            // rule): simple interest over 600 months at 9% gives total interest 4.50
            // with a rounded per-row share round(4.50/600 = 0.0075) = 0.01, equal to
            // the 0.01 nominal installment, so the first 599 rows amortize NO
            // principal (pay 0.01 = 0.00 principal + 0.01 interest on a constant
            // 1.00 balance). Those 599 rows would accumulate 5.99 of interest against
            // the contractual 4.50, so the negative residual (-1.49) is absorbed by
            // the existing walk-back branch, cancelling the interest of the last 149
            // non-final rows; the final row then repays the entire principal (1.00)
            // with zero interest. Rules 1-8 hold numerically.
            var service = CreateService(nameof(CalculateLoan_DegenerateSimpleInterest_OneDinarAt9Percent_600Months_WalkBackBalloon));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1, AnnualInterestRate = 9, DurationMonths = 600,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            });

            Assert.Equal(600, result.Schedule.Count);
            Assert.Equal(0.01m, result.InstallmentAmount);
            Assert.Equal(4.50m, result.TotalInterest);
            Assert.Equal(5.50m, result.TotalRepayment);
            Assert.Equal(450, result.Schedule.Count(r => r.PaymentAmount == 0.01m));
            Assert.Equal(149, result.Schedule.Count(r => r.PaymentAmount == 0.00m));
            var final = result.Schedule.Last();
            Assert.Equal(1.00m, final.PaymentAmount);
            Assert.Equal(1.00m, final.PrincipalPaid);
            Assert.Equal(0m, final.InterestPaid);
            Assert.Equal(0m, final.RemainingBalance);
            AssertScheduleInvariants(result, 1.00m, 0.01m);
        }

        [Theory]
        [InlineData(InterestType.Simple)]
        [InlineData(InterestType.Reducing)]
        public void CalculateLoan_DegenerateSmallPrincipal_OneDinarAt6Percent_600Months_PaysOffEarly(InterestType interestType)
        {
            // Documented degenerate case (FIN-01A/01B-R owner notice, NOT an approved
            // rule): at 6% both rounding paths place the whole 0.01 nominal
            // installment in principal, so the loan pays off after 100 rows and the
            // remaining rows are zero-value installments. For simple interest the
            // untouched contractual residual (3.00 = round(1 * 6% * 50y)) then
            // charges on the final row against a zero balance (interest
            // concentration); for reducing interest every per-period interest on the
            // tiny balances rounds to 0.00, so total interest is 0.00.
            // Rules 1-8 hold numerically; a minimum-installment validation remains an
            // owner decision.
            var service = CreateService($"{nameof(CalculateLoan_DegenerateSmallPrincipal_OneDinarAt6Percent_600Months_PaysOffEarly)}_{interestType}");
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1, AnnualInterestRate = 6, DurationMonths = 600,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = interestType
            });

            Assert.Equal(600, result.Schedule.Count);
            Assert.Equal(0.01m, result.InstallmentAmount);
            Assert.Equal(100, result.Schedule.Take(100).Count(r =>
                r.PaymentAmount == 0.01m && r.PrincipalPaid == 0.01m && r.InterestPaid == 0.00m));
            Assert.Equal(1.00m, result.Schedule.Take(100).Sum(r => r.PrincipalPaid));
            Assert.Equal(0m, result.Schedule[99].RemainingBalance);

            if (interestType == InterestType.Simple)
            {
                Assert.Equal(3.00m, result.TotalInterest);
                Assert.Equal(4.00m, result.TotalRepayment);
                Assert.Equal(499, result.Schedule.Count(r => r.PaymentAmount == 0.00m));
                var final = result.Schedule.Last();
                Assert.Equal(3.00m, final.InterestPaid);
                Assert.Equal(0.00m, final.PrincipalPaid);
                Assert.Equal(3.00m, final.PaymentAmount);
                Assert.Equal(0m, final.RemainingBalance);
            }
            else
            {
                Assert.Equal(0.00m, result.TotalInterest);
                Assert.Equal(1.00m, result.TotalRepayment);
                Assert.Equal(500, result.Schedule.Count(r => r.PaymentAmount == 0.00m));
            }

            AssertScheduleInvariants(result, 1.00m, 0.01m);
        }

        [Theory]
        [InlineData(InterestType.Simple)]
        [InlineData(InterestType.Reducing)]
        public void CalculateLoan_DegenerateSmallPrincipal_ZeroRate_600Months_ZeroRegularInstallments(InterestType interestType)
        {
            // Documented degenerate case (FIN-01A/01B-R owner notice, NOT an approved
            // rule): a 0% loan of 1 JOD over 600 months rounds the nominal
            // installment to 0.00 (round(1/600) with ToEven), producing 599
            // zero-value rows; the entire principal is repaid in the final row.
            // Behavior is identical for both interest types and all invariants hold
            // numerically; a minimum-installment validation remains an owner
            // decision.
            var service = CreateService($"{nameof(CalculateLoan_DegenerateSmallPrincipal_ZeroRate_600Months_ZeroRegularInstallments)}_{interestType}");
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1, AnnualInterestRate = 0, DurationMonths = 600,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = interestType
            });

            Assert.Equal(600, result.Schedule.Count);
            Assert.Equal(0m, result.InstallmentAmount);
            Assert.Equal(0m, result.TotalInterest);
            Assert.Equal(1.00m, result.TotalRepayment);
            Assert.Equal(599, result.Schedule.Count(r => r.PaymentAmount == 0.00m));
            var final = result.Schedule.Last();
            Assert.Equal(1.00m, final.PrincipalPaid);
            Assert.Equal(0m, final.InterestPaid);
            Assert.Equal(0m, final.RemainingBalance);
            AssertScheduleInvariants(result, 1.00m);
        }

        [Fact]
        public void CalculateLoan_SimpleInterest_93_24At6p5Percent_120Months_WalkBackAbsorbsNegativeResidual()
        {
            // Negative-residual walk-back regression (FIN-01B-R Step 3). Existing
            // implemented behavior requiring owner policy acknowledgment, NOT an
            // approved business rule. For simple interest the contractual target is
            // round(93.24 * 6.5% * 10y) = 60.61, and the rounded per-row share
            // round(0.505050...) = 0.51 over-charges each of the 119 non-final rows
            // by ~0.005, so they accumulate 60.69 > 60.61. A negative residual
            // (-0.08) cannot be allocated to the final installment without negative
            // interest, so the walk-back branch removes it from the last non-final
            // row(s): row 119 charges 0.43 instead of 0.51 and pays 1.20 instead of
            // 1.28. Reachable with ordinary inputs under the seeded 6.5% band.
            var service = CreateService(nameof(CalculateLoan_SimpleInterest_93_24At6p5Percent_120Months_WalkBackAbsorbsNegativeResidual));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 93.24m, AnnualInterestRate = 6.5m, DurationMonths = 120,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            });

            Assert.Equal(120, result.Schedule.Count);
            Assert.Equal(1.28m, result.InstallmentAmount);   // round((93.24 + 60.606)/120) = round(1.28205)
            Assert.Equal(60.61m, result.TotalInterest);      // contractual target preserved
            Assert.Equal(153.85m, result.TotalRepayment);    // 93.24 + 60.61
            Assert.Equal(118, result.Schedule.Count(r => r.PaymentAmount == 1.28m));
            var adjusted = result.Schedule.Single(r => r.PaymentNumber == 119);
            Assert.Equal(1.20m, adjusted.PaymentAmount);     // walk-back below the nominal
            Assert.Equal(0.77m, adjusted.PrincipalPaid);
            Assert.Equal(0.43m, adjusted.InterestPaid);      // 0.51 - 0.08
            var final = result.Schedule.Last();
            Assert.Equal(1.61m, final.PaymentAmount);        // 93.24 - 119 * 0.77
            Assert.Equal(1.61m, final.PrincipalPaid);
            Assert.Equal(0m, final.InterestPaid);
            Assert.Equal(0m, final.RemainingBalance);
            AssertScheduleInvariants(result, 93.24m, 1.28m);
        }

        [Fact]
        public void CalculateLoan_SimpleInterest_3_02At6p5Percent_100Months_WalkBackAbsorbsNegativeResidual()
        {
            // Negative-residual walk-back regression (FIN-01B-R Step 3). Existing
            // implemented behavior requiring owner policy acknowledgment, NOT an
            // approved business rule. Total interest 1.64 (round(3.02 * 6.5% *
            // (100/12y))); the rounded per-row share round(0.0164) = 0.02 over-charges
            // the 99 non-final rows (1.98 > 1.64), so the negative residual (-0.34)
            // is absorbed by zeroing the interest of the last 17 non-final rows; the
            // final row repays the remaining 0.05 of principal with zero interest.
            // Reachable with ordinary inputs under the seeded 6.5% band.
            var service = CreateService(nameof(CalculateLoan_SimpleInterest_3_02At6p5Percent_100Months_WalkBackAbsorbsNegativeResidual));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 3.02m, AnnualInterestRate = 6.5m, DurationMonths = 100,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            });

            Assert.Equal(100, result.Schedule.Count);
            Assert.Equal(0.05m, result.InstallmentAmount);   // round((3.02 + 1.636)/100) = round(0.04656)
            Assert.Equal(1.64m, result.TotalInterest);       // contractual target preserved
            Assert.Equal(4.66m, result.TotalRepayment);      // 3.02 + 1.64
            Assert.Equal(83, result.Schedule.Count(r => r.PaymentAmount == 0.05m)); // rows 1..82 + final
            Assert.Equal(0.02m, result.Schedule[81].InterestPaid);
            Assert.Equal(17, result.Schedule.Count(r => r.PaymentAmount == 0.03m)); // rows 83..99 (walk-back)
            Assert.All(result.Schedule.Skip(82).Take(17), r =>
            {
                Assert.Equal(0.03m, r.PaymentAmount);
                Assert.Equal(0.03m, r.PrincipalPaid);
                Assert.Equal(0m, r.InterestPaid);
            });
            var final = result.Schedule.Last();
            Assert.Equal(0.05m, final.PaymentAmount);
            Assert.Equal(0.05m, final.PrincipalPaid);        // 3.02 - 99 * 0.03
            Assert.Equal(0m, final.InterestPaid);
            Assert.Equal(0m, final.RemainingBalance);
            AssertScheduleInvariants(result, 3.02m, 0.05m);
        }

        [Fact]
        public void CalculateLoan_InputWithMoreThanTwoDecimals_SchedulesAtMonetaryPrecision()
        {
            // Principal of 1000.555 is scheduled at its monetary precision (1000.56
            // with ToEven); the interest formula itself still uses the submitted
            // value. Persistence keeps the submitted value only up to the
            // decimal(12,2) column: SQL Server rounds the stored PrincipalAmount
            // half away from zero (1000.555 -> 1000.56, matching ToEven here; a tie
            // input like 1000.545 would store 1000.55 while the schedule sums to
            // 1000.54). No new validation policy is introduced; see FIN-01B-R Step 5
            // for the pending owner decision.
            var service = CreateService(nameof(CalculateLoan_InputWithMoreThanTwoDecimals_SchedulesAtMonetaryPrecision));
            var result = service.CalculateLoan(new LoanInputModel
            {
                PrincipalAmount = 1000.555m, AnnualInterestRate = 6.5m, DurationMonths = 5,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            });

            AssertScheduleInvariants(result, 1000.56m);
        }

        [Fact]
        public async Task LoansService_PersistsTotalsConsistentWithScheduleRows()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(nameof(LoansService_PersistsTotalsConsistentWithScheduleRows))
                .Options;
            using var context = new AppDbContext(options);
            var calculation = new LoanCalculationService(context, new MemoryCache(new MemoryCacheOptions()));
            var loans = new LoansService(context);

            var input = new LoanInputModel
            {
                PrincipalAmount = 1000, AnnualInterestRate = 6.5m, DurationMonths = 5,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Reducing
            };
            var calculationResult = calculation.CalculateLoan(input);

            var entity = await loans.CreateLoanApplicationAsync(
                clientId: 1, sessionId: null, loanCategory: LoanCategory.SeasonalShortTerm,
                input: input, authoritativeAnnualInterestRate: 6.5m, calculationResult: calculationResult);

            Assert.Equal(calculationResult.Schedule.Count, entity.NumberOfPayments);
            Assert.Equal(calculationResult.InstallmentAmount, entity.InstallmentAmount);
            Assert.Equal(calculationResult.TotalInterest, entity.TotalInterest);
            Assert.Equal(1000m, entity.PaymentSchedules.Sum(s => s.PrincipalPaid));
            Assert.Equal(entity.PaymentSchedules.Sum(s => s.InterestPaid), entity.TotalInterest);
            Assert.Equal(entity.PaymentSchedules.Sum(s => s.PaymentAmount),
                entity.PaymentSchedules.Sum(s => s.PrincipalPaid) + entity.PaymentSchedules.Sum(s => s.InterestPaid));
            Assert.All(entity.PaymentSchedules, s => Assert.All(
                new[] { s.PaymentAmount, s.PrincipalPaid, s.InterestPaid, s.RemainingBalance },
                v => Assert.Equal(0m, v % 0.01m)));
            Assert.Equal(0m, entity.PaymentSchedules.Last().RemainingBalance);
        }
    }
}
