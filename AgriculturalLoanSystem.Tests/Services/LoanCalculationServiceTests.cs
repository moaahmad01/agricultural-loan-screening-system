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
    }
}
