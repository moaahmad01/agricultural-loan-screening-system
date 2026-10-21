using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Models.ViewModels;
using AgriculturalLoanSystem.Services.Interfaces;
using AgriculturalLoanSystem.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AgriculturalLoanSystem.Controllers
{
    [Authorize]
    public class LoansController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILoanCalculationService _loanCalculationService;
        private readonly ILoansService _loansService;
        private readonly IClientsService _clientsService;
        private readonly ILogger<LoansController> _logger;

        public LoansController(
            ILoanCalculationService loanCalculationService,
            ILoansService loansService,
            IClientsService clientsService,
            UserManager<IdentityUser> userManager,
            ILogger<LoansController> logger)
        {
            _loanCalculationService = loanCalculationService;
            _loansService = loansService;
            _clientsService = clientsService;
            _userManager = userManager;
            _logger = logger;
        }

        // (SEC-02 / موديل 4، بند 4.1): نفس نمط isStaff || isOwner من الموديلات السابقة —
        // كان LoansController بالكامل بلا أي حماية إطلاقًا قبل الخطوة الصفرية.
        private async Task<bool> IsAuthorizedForClientAsync(int clientId)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Employee")) return true;

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return false;

            var client = await _clientsService.GetClientEntityAsync(clientId);
            return client != null && client.ApplicationUserId == userId;
        }

        [HttpGet]
        public async Task<IActionResult> Calculate(int clientId, int? sessionId)
        {
            if (clientId == 0) return RedirectToAction("Search", "Clients");
            if (!await IsAuthorizedForClientAsync(clientId)) return Forbid();

            if (sessionId.HasValue)
            {
                bool loanExists = await _loansService.LoanExistsForSessionAsync(sessionId.Value);
                if (loanExists)
                {
                    TempData["ErrorMessage"] = "لا يمكن إنشاء قرض جديد. يوجد طلب قرض مسجل بالفعل لهذه الجلسة.";
                    return RedirectToAction("Details", "Clients", new { clientId });
                }
            }

            var activePreviousLoan = await _loansService.GetActiveOngoingLoanAsync(clientId);
            var clientName = await _loansService.GetClientNameAsync(clientId);

            var viewModel = new LoanCalculatorViewModel
            {
                ClientId = clientId,
                SessionId = sessionId,
                ClientName = clientName ?? "عميل غير معروف",
                Input = new LoanInputModel
                {
                    PaymentFrequency = PaymentFrequency.Monthly,
                    InterestType = InterestType.Simple,
                    DurationMonths = 12
                }
            };

            if (activePreviousLoan != null)
            {
                viewModel.HasActivePreviousLoan = true;
                viewModel.PreviousLoanWarning = $"تنبيه: يوجد قرض سابق جارٍ لهذا العميل بقيمة ({activePreviousLoan.LoanAmount}) JOD.";
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLoan(LoanCalculatorViewModel model)
        {
            if (!await IsAuthorizedForClientAsync(model.ClientId)) return Forbid();

            if (model.SessionId.HasValue)
            {
                bool exists = await _loansService.LoanExistsForSessionAsync(model.SessionId.Value);
                if (exists)
                {
                    TempData["ErrorMessage"] = "عذراً، تم تسجيل طلب لهذه الجلسة مسبقاً.";
                    return RedirectToAction("Details", "Clients", new { clientId = model.ClientId });
                }
            }

            if (!ModelState.IsValid)
            {
                return View("Calculate", model);
            }

            try
            {
                // (SEC-04 — الإصلاح الأهم بهذا الموديل): لم يعُد الخادم يثق بـ
                // model.Input.AnnualInterestRate القادم من نموذج العميل إطلاقًا (كان بالإمكان
                // إرسال أي معدل فائدة، حتى 0%، عبر تعديل الطلب مباشرة). المعدل المُعتمَد الوحيد
                // الآن هو الناتج عن إعادة حسابه من الخادم بناءً على فئة القرض والمبلغ، بمطابقة
                // فعلية مع جدول InterestRates — بالضبط الدالة الموجودة أصلاً في الخدمة ولم تكن
                // تُستدعى في هذا المسار إطلاقًا رغم وجودها.
                decimal authoritativeRate = _loanCalculationService.GetInterestRate(model.LoanCategory, model.Input.PrincipalAmount);
                model.Input.AnnualInterestRate = authoritativeRate;

                var calculationResult = _loanCalculationService.CalculateLoan(model.Input);

                var loanEntity = await _loansService.CreateLoanApplicationAsync(
                    model.ClientId,
                    model.SessionId,
                    model.LoanCategory,
                    model.Input,
                    authoritativeRate,
                    calculationResult);

                TempData["SuccessMessage"] = "تم إرسال طلب القرض بنجاح!";
                return RedirectToAction("Details", "Clients", new { clientId = model.ClientId });
            }
            catch (ArgumentException ex)
            {
                // (CQ-04): هذا الاستثناء يأتي من فحص LoanCalculationService الدفاعي (مثلاً مدة
                // تنتج صفر دفعات) — رسالته آمنة للعرض مباشرة لأنها مكتوبة بلغة صديقة للمستخدم أصلاً.
                ModelState.AddModelError("", ex.Message);
                return View("Calculate", model);
            }
            catch (Exception ex)
            {
                // (ARCH-05 — إصلاح): لم نعُد نُظهر ex.Message الخام للمستخدم (كان يُسرّب تفاصيل
                // تقنية داخلية كأسماء الجداول ورسائل SQL). التفاصيل الكاملة تُسجَّل الآن في الـ Log
                // فقط، بينما يرى المستخدم رسالة عامة آمنة.
                _logger.LogError(ex, "فشل إنشاء طلب قرض للعميل {ClientId}", model.ClientId);
                ModelState.AddModelError("", "حدث خطأ أثناء حفظ طلب القرض، يرجى المحاولة مرة أخرى.");
                return View("Calculate", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ApplicationDetails(int loanId)
        {
            var loan = await _loansService.GetApplicationDetailsAsync(loanId);
            if (loan == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isStaff = User.IsInRole("Admin") || User.IsInRole("Employee");
            bool isOwner = loan.Client != null && loan.Client.ApplicationUserId == userId;

            if (!isStaff && !isOwner)
            {
                return Forbid();
            }

            return View(loan);
        }
    }
}
