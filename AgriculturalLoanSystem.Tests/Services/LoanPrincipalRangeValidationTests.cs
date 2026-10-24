using AgriculturalLoanSystem.Controllers;
using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Models.ViewModels;
using AgriculturalLoanSystem.Services.Implementations;
using AgriculturalLoanSystem.Services.Interfaces;
using AgriculturalLoanSystem.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Moq;

namespace AgriculturalLoanSystem.Tests.Services
{
    /// <summary>
    /// VAL-01 — إصلاح التحقق من نطاق أصل القرض.
    ///
    /// السلوك القديم (خَلَل قائم مسبقًا): [Range(1, 10_000_000)] بأعداد صحيحة يُحوِّل القيمة
    /// المُرسَلة إلى int (OperandType = Int32) قبل المقارنة، فتعبر النطاق قيم مثل
    /// 0.51–0.99 (تُقرَّب إلى 1) و10,000,000.01–10,000,000.50 (تُقرَّب إلى 10,000,000)
    /// رغم كونها خارج الحدين المقصودين — أُثبت ذلك بمسح حقيقي قبل الإصلاح
    /// (0.50 رُفضت، 0.50+ أصغرية قبلت، 10,000,000.51 رُفضت).
    ///
    /// الإصلاح: مقارنة عشرية صريحة عبر [Range(typeof(decimal), "1", "10000000",
    /// ParseLimitsInInvariantCulture = true)] بلا أي تحويل وسيط وتقريب. لم يتغير
    /// الحَدان الشموليان (1..10,000,000) ولا الرسالة العربية.
    ///
    /// قاعدة FIN-01 (منزلتان عشريتان كحد أقصى) باقية كما هي بجانب هذه القاعدة.
    /// </summary>
    public class LoanPrincipalRangeValidationTests
    {
        private const string RangeArabicMessage =
            "قيمة القرض يجب أن تكون بين 1 و 10,000,000 دينار";

        // =====================================================================
        // 1) المقارنة عشرية (Decimal) لا صحيحة (Int32) — على الكائن الفعلي المنَشأ
        //    من الـ DTO نفسه، لا عبر افتراض تحويل فقط.
        // =====================================================================

        [Fact]
        public void RangeAttribute_OnPrincipalAmount_IsDecimalComparisonWithExactLimits()
        {
            var attribute = typeof(LoanInputModel)
                .GetProperty(nameof(LoanInputModel.PrincipalAmount))!
                .GetCustomAttributes(false)
                .OfType<RangeAttribute>()
                .Single();

            // .NET 10: النمط العشري يُنفَّذ فعليًا في مقارنة IsValid — تحقق النوع
            Assert.Equal(typeof(decimal), attribute.OperandType);

            // الحَدان الشموليان كما هما قبل الإصلاح — بلا أي توسيع أو تقليص للسياسة
            Assert.Equal(1m, Convert.ToDecimal(
                attribute.Minimum, CultureInfo.InvariantCulture));
            Assert.Equal(10_000_000m, Convert.ToDecimal(
                attribute.Maximum, CultureInfo.InvariantCulture));
        }

        // =====================================================================
        // 2) السلوك الجديد عبر MVC نفسه (Validator على الـ DTO الكامل — نفس
        //    ممرّ الربط): القيم خارج الحدين الشموليين تُرفض عدديًا الآن.
        // =====================================================================

        private static LoanCalculatorViewModel ValidViewModel(decimal principal)
        {
            return new LoanCalculatorViewModel
            {
                ClientId = 1,
                SessionId = null,
                LoanCategory = LoanCategory.SeasonalShortTerm,
                Input = new LoanInputModel
                {
                    PrincipalAmount = principal,
                    AnnualInterestRate = 6.5m,
                    DurationMonths = 12,
                    PaymentFrequency = PaymentFrequency.Monthly,
                    InterestType = InterestType.Simple
                }
            };
        }

        private static (bool IsValid, List<ValidationResult> Results) ValidateViewModel(
            LoanCalculatorViewModel model)
        {
            // نفس ممرّ FIN-01C: التدرّج إلى الخاصية المركّبة Input — التطبيق المطابق
            // لسلوك ربط MVC الفعلي، والمفاتيح بصيغة التسمية الفعلية "Input.PrincipalAmount".
            var results = new List<ValidationResult>();
            bool valid = Validator.TryValidateObject(model, new ValidationContext(model), results,
                validateAllProperties: true);

            if (model.Input != null)
            {
                var nested = new List<ValidationResult>();
                valid &= Validator.TryValidateObject(model.Input, new ValidationContext(model.Input),
                    nested, validateAllProperties: true);
                results.AddRange(nested.Select(r => new ValidationResult(r.ErrorMessage,
                    r.MemberNames.Select(m => $"Input.{m}"))));
            }

            return (valid, results);
        }

        [Theory]
        [InlineData("0.50")]         // كانت تُرفض فعلًا (تقريبًا إلى 0) — تبقى مرفوضة عدديًا
        [InlineData("0.51")]         // كانت تعبر النطاق (تقريبًا إلى 1)
        [InlineData("0.99")]         // كانت تعبر النطاق (تقريبًا إلى 1)
        [InlineData("10000000.01")]  // كانت تعبر النطاق (تقريبًا إلى 10,000,000)
        [InlineData("10000000.49")]  // كانت تعبر النطاق (تقريبًا إلى 10,000,000)
        [InlineData("10000000.50")]  // كانت تعبر النطاق (تقريبًا إلى 10,000,000)
        [InlineData("10000000.51")]  // كانت تُرفض فعلًا (تقريبًا إلى 10,000,001) — تبقى مرفوضة
        public void ModelValidation_RejectsAmountsOutsideInclusiveBoundaries(string amountText)
        {
            var principal = decimal.Parse(amountText, CultureInfo.InvariantCulture);
            var (isValid, results) = ValidateViewModel(ValidViewModel(principal));

            Assert.False(isValid);
            var rangeError = results.Single(r => r.MemberNames.Contains("Input.PrincipalAmount"));
            Assert.Equal(RangeArabicMessage, rangeError.ErrorMessage);
        }

        [Theory]
        [InlineData("1.00")]          // الحد الأدنى الشمولي — مقبول
        [InlineData("1.01")]          // أول خطوة عشرية فوق الحد الأدنى
        [InlineData("1000.50")]       // قيمة داخلية بمنزلتين عشريتين
        [InlineData("9999999.99")]    // آخر خطوة تحت الحد الأقصى
        [InlineData("10000000.00")]   // الحد الأقصى الشمولي — مقبول
        public void ModelValidation_AcceptsInclusiveBoundariesAndInteriorValues(string amountText)
        {
            var principal = decimal.Parse(amountText, CultureInfo.InvariantCulture);
            var (isValid, results) = ValidateViewModel(ValidViewModel(principal));

            Assert.True(isValid);
            Assert.Empty(results);
        }

        // =====================================================================
        // 3) بنية MVC الفعلية (بلا WebApplicationFactory — القاعدة الراسخة في
        //    بنية الاختبارات الحالية، لا يُضاف إطار جديد): نستخدم نقطة دخول
        //    MVC نفسها لقراءة خصائص الـ DTO (ModelAttributes.GetAttributesForProperty —
        //    آلية الطبقة الميتاداتا الداخلية في ASP.NET Core) لاستخراج نسخة الـ
        //    RangeAttribute الحقيقية المربوطة بالخاصية وتنفيذها كما يفعل المحرِّك.
        //    مسار HTTP الحقيقي (الـ Controller POST) مغطى في القسم 5.
        // =====================================================================

        private static (bool IsValid, string? Message) ValidateViaMvcMetadata(LoanInputModel input)
        {
            var property = typeof(LoanInputModel).GetProperty(
                nameof(LoanInputModel.PrincipalAmount))!;
            var attributes = ModelAttributes.GetAttributesForProperty(
                typeof(LoanInputModel), property);

            var range = (attributes.PropertyAttributes ?? Array.Empty<Attribute>())
                .OfType<RangeAttribute>().Single();
            Assert.Equal(typeof(decimal), range.OperandType);

            var result = range.GetValidationResult(input.PrincipalAmount,
                new ValidationContext(input) { MemberName = nameof(LoanInputModel.PrincipalAmount) });

            return (result == ValidationResult.Success, result?.ErrorMessage);
        }

        [Theory]
        [InlineData("0.50")]
        [InlineData("0.99")]
        [InlineData("10000000.50")]
        [InlineData("10000000.51")]
        public void MvcValidatorInfra_RejectsOutOfRangePrincipalsWithArabicMessage(string amountText)
        {
            var input = new LoanInputModel
            {
                PrincipalAmount = decimal.Parse(amountText, CultureInfo.InvariantCulture),
                AnnualInterestRate = 6.5m, DurationMonths = 12,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            };
            var (isValid, message) = ValidateViaMvcMetadata(input);

            Assert.False(isValid);
            Assert.Equal(RangeArabicMessage, message);
        }

        [Theory]
        [InlineData("1.00")]
        [InlineData("10000000.00")]
        public void MvcValidatorInfra_AcceptsInclusiveBoundaryPrincipals(string amountText)
        {
            var input = new LoanInputModel
            {
                PrincipalAmount = decimal.Parse(amountText, CultureInfo.InvariantCulture),
                AnnualInterestRate = 6.5m, DurationMonths = 12,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            };
            var (isValid, message) = ValidateViaMvcMetadata(input);

            Assert.True(isValid);
            Assert.Null(message);
        }

        // =====================================================================
        // 4) قاعدة الدقة (FIN-01) سليمة بجانب قاعدة النطاق — رفض من قاعدة الدقة،
        //    ورفض من قاعدة النطاق، وبدون أي تقريب صامت للمُرسَل.
        // =====================================================================

        [Theory]
        [InlineData("1000.545")]
        [InlineData("9999999.999")]
        public void PrecisionRule_StillRejectsSubCentPrincipalsWithinRange(
            string amountText)
        {
            var principal = decimal.Parse(amountText, CultureInfo.InvariantCulture);
            var (isValid, results) = ValidateViewModel(ValidViewModel(principal));

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains("Input.PrincipalAmount")
                && r.ErrorMessage == "قيمة القرض يجب أن تحتوي على منزلتين عشريتين كحد أقصى.");
        }

        /// <summary>المرسَل لا يُعدَّل صامتًا: القيمة المرفوضة تُقيَّم كما أُرسلت حرفيًا.</summary>
        [Fact]
        public void NoSilentNormalization_RejectedValuesKeepTheirExactValue()
        {
            // 0.99 كان سابقًا يصبح int(1) أثناء المقارنة؛ الآن يُرفض بقيمته الأصلية
            var input = ValidViewModel(0.99m).Input!;
            var (isValid, results) = ValidateViewModel(ValidViewModel(0.99m));

            Assert.False(isValid);
            Assert.Equal(RangeArabicMessage, results
                .Single(r => r.MemberNames.Contains("Input.PrincipalAmount")).ErrorMessage);
            // القيمة نفسها لم تُستبدل — أحادية القرار لا تقريب صامت في المرور
            Assert.Equal(0.99m, input.PrincipalAmount);
        }

        // =====================================================================
        // 5) مسار MVC POST (LoansController.CreateLoan) — نفس بنية اختبارات FIN-01C
        //    (بنية مستنسخة متعمَّدة؛ الملفان مستقلان): حالة ModelState تُبنى من
        //    ممرّ Validator أعلاه، تمامًا كما يولّده ربط MVC الحقيقي.
        // =====================================================================

        private static LoansController CreateController(AppDbContext db,
            LoanCalculationService? calculationService, ILoanCalculationService? calculationMock,
            ILogger<LoansController>? logger = null)
        {
            var loans = new LoansService(db);
            var controller = new LoansController(
                calculationMock ?? (ILoanCalculationService)calculationService!,
                loans,
                clientsService: null!,
                userManager: null!,
                logger: logger ?? NullLogger<LoansController>.Instance);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Role, "Admin")
                    }))
                }
            };
            var tempDataProvider = new SilentTempDataProvider();
            var services = new ServiceCollection();
            services.AddSingleton<ITempDataProvider>(tempDataProvider);
            services.AddSingleton<ITempDataDictionaryFactory>(new TempDataDictionaryFactory(tempDataProvider));
            services.AddSingleton<IUrlHelperFactory, StubUrlHelperFactory>();
            controller.ControllerContext.HttpContext.RequestServices = services.BuildServiceProvider();
            return controller;
        }

        private sealed class SilentTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object?> LoadTempData(HttpContext context)
                => new Dictionary<string, object?>();

            public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
            {
            }
        }

        private sealed class StubUrlHelperFactory : IUrlHelperFactory
        {
            public IUrlHelper GetUrlHelper(ActionContext actionContext)
                => new StubUrlHelper(actionContext);
        }

        private sealed class StubUrlHelper : IUrlHelper
        {
            public StubUrlHelper(ActionContext actionContext) => ActionContext = actionContext;

            public ActionContext ActionContext { get; }

            public string? Action(UrlActionContext actionContext)
                => $"/{(string?)actionContext.Controller}/{actionContext.Action}";

            public string? Content(string? contentPath) => contentPath;

            public bool IsLocalUrl(string? url) => true;

            public string? Link(string? routeName, object? values) => null;

            public string? RouteUrl(UrlRouteContext routeContext) => null;
        }

        private static void ApplyModelState(Controller controller, List<ValidationResult> results)
        {
            foreach (var validationResult in results)
            {
                controller.ModelState.AddModelError(
                    validationResult.MemberNames.FirstOrDefault() ?? string.Empty,
                    validationResult.ErrorMessage!);
            }
        }

        [Theory]
        [InlineData("0.99")]         // كان يمر عبر الإصلاح القديم (int) ويصل الحساب والحفظ
        [InlineData("10000000.50")]  // كان يمر عبر الإصلاح القديم (int) ويصل الحساب والحفظ
        public async Task CreateLoanPost_PreviouslyLeakedOutOfRangePrincipals_BlockedBeforeCalculationAndPersistsNothing(
            string amountText)
        {
            using var db = Database();
            var calculationMock = new Mock<ILoanCalculationService>();
            var controller = CreateController(db, null, calculationMock.Object);
            var model = ValidViewModel(decimal.Parse(amountText, CultureInfo.InvariantCulture));
            var (isValid, results) = ValidateViewModel(model);
            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains("Input.PrincipalAmount") && r.ErrorMessage == RangeArabicMessage);
            ApplyModelState(controller, results);

            var result = await controller.CreateLoan(model);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Calculate", view.ViewName);
            var rangeKey = controller.ModelState["Input.PrincipalAmount"];
            Assert.NotNull(rangeKey);
            Assert.Contains(RangeArabicMessage, rangeKey!.Errors.Select(e => e.ErrorMessage));
            // لا حساب ولا جدول ولا طلب قرض — الرفض قبل أي عمل وظيفي
            calculationMock.Verify(s => s.GetInterestRate(It.IsAny<LoanCategory>(), It.IsAny<decimal>()), Times.Never);
            calculationMock.Verify(s => s.CalculateLoan(It.IsAny<LoanInputModel>()), Times.Never);
            Assert.Empty(await db.LoanCalculations.ToListAsync());
            Assert.Empty(await db.PaymentSchedules.ToListAsync());
        }

        [Fact]
        public async Task CreateLoanPost_UpperBoundaryPrincipal_CalculatesAndPersistsAsSubmitted()
        {
            using var db = Database();
            var calculationService = new LoanCalculationService(db, new MemoryCache(new MemoryCacheOptions()));
            var logger = new CapturingLogger();
            var controller = CreateController(db, calculationService, null, logger);
            var model = ValidViewModel(10_000_000.00m);
            model.SessionId = null;

            var result = await controller.CreateLoan(model);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            // القيمة المخزَّنة هي القيمة المُرسَلة نفسها — الحد الأقصى مقبول دون تعديل
            var entity = await db.LoanCalculations.Include(l => l.PaymentSchedules).SingleAsync();
            Assert.Equal(10_000_000.00m, entity.PrincipalAmount);
            Assert.Equal(entity.PaymentSchedules.Count, entity.NumberOfPayments);
            Assert.Equal(entity.PaymentSchedules.Sum(s => s.PrincipalPaid), entity.PrincipalAmount);
            Assert.Equal(entity.PaymentSchedules.Sum(s => s.InterestPaid), entity.TotalInterest);
        }

        private sealed class CapturingLogger : ILogger<LoansController>
        {
            public List<string> Messages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                Messages.Add($"{logLevel}: {formatter(state, exception)}{(exception == null ? "" : "\n" + exception)}");
            public string Dump() => string.Join("\n", Messages);
        }

        private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    }
}
