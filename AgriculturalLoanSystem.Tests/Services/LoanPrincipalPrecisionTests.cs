using AgriculturalLoanSystem.Controllers;
using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Models.ViewModels;
using AgriculturalLoanSystem.Services.Implementations;
using AgriculturalLoanSystem.Services.Interfaces;
using AgriculturalLoanSystem.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    /// FIN-01C — Option A (قرار المالك): التحقق الخادمي من دقة أصل القرض.
    ///
    /// السياسة: قيمة أصل القرض المُرسَلة يجب أن تكون قابلة للتمثيل بدقة نقدية منزلتين
    /// عشريتين (0.01 دينار)؛ تُرفض القيم الأدق مثل 1000.545 من دون أي تقريب صامت —
    /// لأن SQL Server يخزّن decimal(12,2) بترقيم "نصف بعيدًا عن الصفر" بينما جدول
    /// السداد يستخدم MidpointRounding.ToEven (FR-01B-R/D5: فرق تخزين بمقدار 0.01 دينار مؤكَّد).
    ///
    /// ملاحظة إثبات (FIN-01D): هذه الاختبارات تعمل على EF InMemory، وهو لا يُطبّق سلوك
    /// التخزين الخاص بـ SQL Server للعمود decimal(12,2)؛ فُحص التخزين الفعلي على قاعدة
    /// بيانات LocalDB معزولة يدويًا مرة واحدة فقط، عبر EnsureCreated (وليس عبر EF
    /// migrations)، ولم يُضَف أي اختبار SQL آلي.
    ///
    /// القاعدة مشتركة بتنفيذ واحد (LoanPrincipalPrecision) وتُطبَّق في موضعين:
    /// 1) LoanPrincipalPrecisionAttribute ضمن MVC ModelState — قبل الحساب وقبل الحفظ.
    /// 2) حارس دفاعي في LoansService.CreateLoanApplicationAsync — أي مسار داخلي بلا MVC.
    /// السلوك الإيجابي المقبول: 1000، 1000.50، 1000.55، و1000.000 (قيمة عددية دقيقة).
    /// </summary>
    public class LoanPrincipalPrecisionTests
    {
        // =====================================================================
        // 1) القاعدة العددية المشتركة — مستقلة عن الثقافة تمامًا.
        // =====================================================================

        [Theory]
        [InlineData("1000")]
        [InlineData("1000.50")]
        [InlineData("1000.55")]
        [InlineData("1000.000")] // قيمة عددية مطابقة تمامًا لمبلغ نقدي
        [InlineData("1000.5")]
        [InlineData("0.01")]
        [InlineData("10000000")]
        public void SharedRule_AcceptsWholeAndTwoDecimalAmounts(string amountText)
        {
            // تحليل نصي صريح بالثقافة الثابتة لإظهار أن القاعدة نفسها لا تعتمد ثقافة.
            Assert.True(LoanPrincipalPrecision.IsRepresentableAtTwoDecimals(
                decimal.Parse(amountText, CultureInfo.InvariantCulture)));
        }

        [Theory]
        [InlineData("1000.545")] // حالة عدم التطابق المخزَّن المؤكدة في FIN-01B-R
        [InlineData("1000.555")]
        [InlineData("1000.001")]
        [InlineData("999.999")]
        [InlineData("10000000.0001")]
        public void SharedRule_RejectsAmountsFinerThanTwoDecimals(string amountText)
        {
            Assert.False(LoanPrincipalPrecision.IsRepresentableAtTwoDecimals(
                decimal.Parse(amountText, CultureInfo.InvariantCulture)));
        }

        // =====================================================================
        // 2) تحقق نموذج MVC (LoanInputModel ضمن LoanCalculatorViewModel):
        //    نفس ما ينفّذه مسار ربط MVC قبل أي كود في الإجراء.
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

        private static (bool IsValid, List<ValidationResult> Results) ValidateViewModel(LoanCalculatorViewModel model)
        {
            // Validator القياسي لا يتدرّج تلقائيًا داخل الخصائص المركّبة، بينما ربط MVC
            // يتدرّج فعلًا إلى Input (الخاصية المركّبة الوحيدة ذات الصلة؛ ClientName عليها
            // ValidateNever وResult غير مرتبط بالمسار هنا). لذا نُجري ممرّين مطابقين
            // لسلوك MVC ونعيد المفاتيح بصيغة تسمية الربط الفعلية: "Input.PrincipalAmount".
            var results = new List<ValidationResult>();
            bool valid = Validator.TryValidateObject(model, new ValidationContext(model), results,
                validateAllProperties: true);

            if (model.Input != null)
            {
                var nested = new List<ValidationResult>();
                valid &= Validator.TryValidateObject(model.Input, new ValidationContext(model.Input), nested,
                    validateAllProperties: true);
                results.AddRange(nested.Select(r => new ValidationResult(r.ErrorMessage,
                    r.MemberNames.Select(m => $"Input.{m}"))));
            }

            return (valid, results);
        }

        [Theory]
        [InlineData(1000)]
        [InlineData(1000.50)]
        [InlineData(1000.55)]
        [InlineData(1)]        // الحد الأدنى الحالي للنطاق — يبقى مقبولًا
        [InlineData(10000000)] // الحد الأقصى الحالي للنطاق — يبقى مقبولًا
        public void ModelValidation_AcceptsWholeAndTwoDecimalPrincipals(decimal principal)
        {
            var (isValid, results) = ValidateViewModel(ValidViewModel(principal));

            Assert.True(isValid);
            Assert.Empty(results);
        }

        [Theory]
        [InlineData(1000.545)]
        [InlineData(1000.555)]
        [InlineData(1000.001)]
        public void ModelValidation_RejectsSubCentPrincipalsWithArabicMessage(decimal principal)
        {
            var (isValid, results) = ValidateViewModel(ValidViewModel(principal));

            Assert.False(isValid);
            var precisionError = results.Single(r => r.MemberNames.Contains("Input.PrincipalAmount"));
            Assert.Equal(LoanPrincipalPrecision.ErrorMessage, precisionError.ErrorMessage);
            // (FIN-01D): تحقق حرفي من نص الرسالة العربية المعروض فعليًا — لا يكفي مقارنة الرسالة
            // بثابتها (ممكن أن ينحرف الثابت نفسه). الصياغة الصحيحة تذكر "منزلتين عشريتين" فقط،
            // بلا أي ادعاء بوحدة قطرية ("فلس") غير دقيقة لأن منزلتين = 0.01 دينار = 10 فلوس.
            Assert.Equal("قيمة القرض يجب أن تحتوي على منزلتين عشريتين كحد أقصى.",
                precisionError.ErrorMessage);
        }

        [Fact]
        public void ModelValidation_PreservesPrincipalRangeAlongsidePrecisionRule()
        {
            // نطاق القرض (1..10,000,000) يبقى مفروضًا كما هو بجانب قاعدة الدقة الجديدة.
            // (VAL-01): حدود [Range] أصبحت مقارنة عشرية صريحة عبر
            // [Range(typeof(decimal), "1", "10000000", ParseLimitsInInvariantCulture = true)]
            // بعد أن كانت المقارنة تمرّ عبر تحويل int (OperandType = Int32) تجعل قيمًا مثل
            // 0.99 و10,000,000.50 تعبر النطاق المقصود. الحَدان الشموليان والرسالة العربية
            // لم يتغيرا، وتفاصيل الحدود موجودة في LoanPrincipalRangeValidationTests.
            var aboveRange = ValidateViewModel(ValidViewModel(10_000_001m));

            Assert.False(aboveRange.IsValid);
            Assert.Contains(aboveRange.Results, r => r.MemberNames.Contains("Input.PrincipalAmount")
                && r.ErrorMessage!.Contains("بين 1 و"));

            // قاعدة الدقة ترفض القيم داخل النطاق وغير القابلة للتمثيل — النطاق لم يُضعف.
            var inRangeSubCent = ValidateViewModel(ValidViewModel(1000.545m));
            Assert.False(inRangeSubCent.IsValid);
        }

        // =====================================================================
        // 3) مسار MVC POST (LoansController.CreateLoan) — بنفس بنية اختبارات
        //    AdminCreationValidationTests (تثبيت الـ Controller مباشرة). حالة
        //    ModelState تُبنى هنا من نتائج Validator أعلاه، أي تمامًا ما كان
        //    سيولّده ربط MVC الفعلي قبل تنفيذ الإجراء. لا يوجد WebApplicationFactory
        //    في بنية الاختبارات الحالية، ولا يُضاف أي إطار اختبار جديد.
        // =====================================================================

        private static LoansController CreateController(AppDbContext db,
            LoanCalculationService? calculationService, ILoanCalculationService? calculationMock,
            ILogger<LoansController>? logger = null)
        {
            var loans = new LoansService(db);
            var controller = new LoansController(
                calculationMock ?? (ILoanCalculationService)calculationService!,
                loans,
                clientsService: null!,   // غير مستخدم على مسار Admin أسفل
                userManager: null!,      // غير مستخدم على مسار Admin أسفل
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
            // TempData يحتاج ITempDataDictionaryFactory داخل RequestServices — بديل تجريبي صامت.
            var tempDataProvider = new SilentTempDataProvider();
            var services = new ServiceCollection();
            services.AddSingleton<ITempDataProvider>(tempDataProvider);
            services.AddSingleton<ITempDataDictionaryFactory>(new TempDataDictionaryFactory(tempDataProvider));
            // المسار يحتاج IUrlHelperFactory لتوليد رابط التحويل — بديل تجريبي يلتقط
            // قرار التحويل نفسه (Action/Controller/RouteValues) دون بنية توجيه كاملة.
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
            public IUrlHelper GetUrlHelper(ActionContext actionContext) => new StubUrlHelper(actionContext);
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

        [Fact]
        public async Task CreateLoanPost_ThreeDecimalPrincipal_RejectedBeforeCalculationAndPersistsNothing()
        {
            // 1000.545 = حالة عدم التطابق المؤكدة FIN-01B-R/D5. POST مرفوض من ModelState
            // (كما في MVC الفعلي) قبل حساب الفائدة وقبل أي إدخال.
            using var db = Database();
            var calculationMock = new Mock<ILoanCalculationService>();
            var controller = CreateController(db, null, calculationMock.Object);
            var model = ValidViewModel(1000.545m);
            var (_, results) = ValidateViewModel(model);
            ApplyModelState(controller, results);

            var result = await controller.CreateLoan(model);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Calculate", view.ViewName);
            var key = controller.ModelState["Input.PrincipalAmount"];
            Assert.NotNull(key);
            Assert.Contains(LoanPrincipalPrecision.ErrorMessage,
                key!.Errors.Select(e => e.ErrorMessage));
            // تنفيذ التحقق قبل الحساب: لم يُستدعَ حساب المعدل ولا الجدول إطلاقًا.
            calculationMock.Verify(s => s.GetInterestRate(It.IsAny<LoanCategory>(), It.IsAny<decimal>()), Times.Never);
            calculationMock.Verify(s => s.CalculateLoan(It.IsAny<LoanInputModel>()), Times.Never);
            // لا طلب قرض ولا جدول سداد.
            Assert.Empty(await db.LoanCalculations.ToListAsync());
            Assert.Empty(await db.PaymentSchedules.ToListAsync());
        }

        [Fact]
        public async Task CreateLoanPost_TwoDecimalPrincipal_CalculatesAndPersistsAsSubmitted()
        {
            // قيمة صالحة (1000.50) تظل تعمل من طرف إلى طرف بالنفس مسار POST الحالي.
            using var db = Database();
            var calculationService = new LoanCalculationService(db, new MemoryCache(new MemoryCacheOptions()));
            var logger = new CapturingLogger();
            var controller = CreateController(db, calculationService, null, logger);
            var model = ValidViewModel(1000.50m);
            model.SessionId = null;

            var result = await controller.CreateLoan(model);

            var redirect = result as RedirectToActionResult;
            Assert.True(redirect != null,
                $"لم يعُد التحويل المتوقع: {result.GetType().Name}\n{logger.Dump()}");
            Assert.Equal("Details", redirect!.ActionName);
            Assert.Equal(model.ClientId, redirect.RouteValues!["clientId"]);
            // القيمة المخزَّنة هي القيمة المُرسَلة نفسها — لا تقريب ولا تعديل.
            var entity = await db.LoanCalculations.Include(l => l.PaymentSchedules).SingleAsync();
            Assert.Equal(1000.50m, entity.PrincipalAmount);
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

        // =====================================================================
        // 4) الحرس الدفاعي في نقطة الحفظ الداخلية (LoansService) — أي نداء لا
        //    يمرّ عبر MVC يُرفض قبل إضافة أي كيان.
        // =====================================================================

        [Theory]
        [InlineData(1000.545)]
        [InlineData(1000.001)]
        public async Task InternalPersistence_RejectsSubCentPrincipal_BeforeAnyInsert(decimal principal)
        {
            using var db = Database();
            var calculation = new LoanCalculationService(db, new MemoryCache(new MemoryCacheOptions()));
            var loans = new LoansService(db);
            var input = new LoanInputModel
            {
                PrincipalAmount = principal, AnnualInterestRate = 6.5m, DurationMonths = 5,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            };
            var calculationResult = calculation.CalculateLoan(input);

            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                loans.CreateLoanApplicationAsync(
                    clientId: 1, sessionId: null, loanCategory: LoanCategory.SeasonalShortTerm,
                    input: input, authoritativeAnnualInterestRate: 6.5m,
                    calculationResult: calculationResult));

            Assert.Equal(LoanPrincipalPrecision.ErrorMessage, exception.Message);
            Assert.Empty(await db.LoanCalculations.ToListAsync());
            Assert.Empty(await db.PaymentSchedules.ToListAsync());
            Assert.Empty(db.ChangeTracker.Entries());
        }

        [Fact]
        public async Task InternalPersistence_ExactMismatchCase_1000_545_IsReportedVerbatim()
        {
            // الحالة المرجعية الدقيقة من FIN-01B-R/D5: 1000.545 كانت تُخزَّن 1000.55
            // (SQL: نصف بعيدًا) بينما جدول السداد 1000.54 (ToEven) — رفض صريح الآن.
            using var db = Database();
            var loans = new LoansService(db);
            var input = new LoanInputModel
            {
                PrincipalAmount = 1000.545m, AnnualInterestRate = 6.5m, DurationMonths = 5,
                PaymentFrequency = PaymentFrequency.Monthly, InterestType = InterestType.Simple
            };

            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                loans.CreateLoanApplicationAsync(
                    clientId: 1, sessionId: null, loanCategory: LoanCategory.SeasonalShortTerm,
                    input: input, authoritativeAnnualInterestRate: 6.5m,
                    calculationResult: new LoanCalculationResultDto()));

            Assert.Equal(LoanPrincipalPrecision.ErrorMessage, exception.Message);
            Assert.Empty(await db.LoanCalculations.ToListAsync());
        }

        private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    }
}
