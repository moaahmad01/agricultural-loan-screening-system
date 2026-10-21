using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Infrastructure;
using AgriculturalLoanSystem.Models.Inputs;
using AgriculturalLoanSystem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AgriculturalLoanSystem.Controllers
{
    [Authorize]
    public class ScreeningController : Controller
    {
        private readonly IScreeningService _screeningService;
        private readonly IClientsService _clientsService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly PrivateDocumentStore _documents;
        private readonly ILogger<ScreeningController> _logger;

        public ScreeningController(
            IScreeningService screeningService,
            IClientsService clientsService,
            UserManager<IdentityUser> userManager,
            PrivateDocumentStore documents,
            ILogger<ScreeningController> logger)
        {
            _screeningService = screeningService;
            _clientsService = clientsService;
            _userManager = userManager;
            _documents = documents;
            _logger = logger;
        }

        // (SEC-03 / موديل 3، بند 3.1): فحص ملكية موحّد يُستخدم في كل Action يتعامل مع بيانات
        // عميل محدَّد — نفس نمط isStaff || isOwner المطبَّق في ClientsController (موديل 2).
        private async Task<bool> IsAuthorizedForClientAsync(int clientId)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Employee")) return true;

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return false;

            var client = await _clientsService.GetClientEntityAsync(clientId);
            return client != null && client.ApplicationUserId == userId;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartNewSession(int clientId)
        {
            if (clientId == 0) return RedirectToAction("Search", "Clients");
            if (!await IsAuthorizedForClientAsync(clientId)) return Forbid();

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            int sessionId = await _screeningService.CreateNewSessionAsync(clientId, userId);

            return RedirectToAction("Instructions", new { clientId, sessionId });
        }

        [HttpGet]
        public async Task<IActionResult> Instructions(int clientId, int sessionId)
        {
            if (clientId == 0) return RedirectToAction("Search", "Clients");
            if (!await IsAuthorizedForClientAsync(clientId)) return Forbid();

            var client = await _screeningService.GetClientBriefAsync(clientId);
            if (client == null) return NotFound();

            ViewBag.SessionId = sessionId;
            return View(client);
        }

        [HttpGet]
        public async Task<IActionResult> Tool(int clientId, int sessionId, int? currentSectionIndex = 0)
        {
            if (clientId == 0 || sessionId == 0) return RedirectToAction("Search", "Clients");

            // (تشديد إضافي): لا نكتفي بالتحقق من clientId المُمرَّر بالرابط بل نتأكد أنه يطابق
            // فعليًا العميل الحقيقي المرتبط بـ sessionId، وإلا كان بالإمكان تمرير sessionId يخص
            // عميلًا آخر مع clientId خاص بالمستخدم نفسه لتجاوز الفحص.
            var actualClientId = await _screeningService.GetClientIdForSessionAsync(sessionId);
            if (actualClientId == null || actualClientId != clientId) return NotFound();
            if (!await IsAuthorizedForClientAsync(clientId)) return Forbid();

            var sections = await _screeningService.GetSectionsAndQuestionsAsync();

            var progress = await _screeningService.GetSessionProgressAsync(sessionId, sections);

            var allDocs = await _screeningService.GetSessionDocumentsAsync(sessionId);
            if (sections.Count == 0) return View("Empty");
            if (currentSectionIndex < 0 || currentSectionIndex >= sections.Count) return BadRequest();
            int currentSecId = sections[currentSectionIndex ?? 0].Id;

            ViewBag.ExistingSectionDocs = allDocs.Where(d => d.SectionId == currentSecId).ToList();
            ViewBag.ClientId = clientId;
            ViewBag.SessionId = sessionId;
            ViewBag.CurrentSectionIndex = currentSectionIndex ?? 0;
            ViewBag.ExistingAnswers = progress.ExistingAnswers;
            ViewBag.CompletedSectionIndexes = progress.CompletedSectionIndexes;

            return View(sections);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessSection(int clientId, int sessionId, int currentSectionIndex, List<AnswerInput> Answers)
        {
            var actualClientId = await _screeningService.GetClientIdForSessionAsync(sessionId);
            if (actualClientId == null || actualClientId != clientId) return NotFound();
            if (!await IsAuthorizedForClientAsync(clientId)) return Forbid();

            if (!await _screeningService.IsSessionEditableAsync(sessionId)) return Conflict();
            var sections = await _screeningService.GetSectionsAndQuestionsAsync();
            if (!ModelState.IsValid || currentSectionIndex < 0 || currentSectionIndex >= sections.Count)
                return BadRequest();
            var allowedIds = sections[currentSectionIndex].Questions.Select(q => q.Id).ToHashSet();
            var validAnswers = new Dictionary<int, AnswerType>();
            foreach (var answer in Answers ?? [])
            {
                if (!allowedIds.Contains(answer.QuestionId) ||
                    !Enum.TryParse<AnswerType>(answer.Answer, true, out var value) ||
                    !Enum.IsDefined(value) || !validAnswers.TryAdd(answer.QuestionId, value))
                    return BadRequest();
            }
            if (validAnswers.Count > 0)
                await _screeningService.SaveSectionAnswersAsync(sessionId, validAnswers);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true });
            }

            bool isLastSection = currentSectionIndex >= sections.Count - 1;

            if (isLastSection)
            {
                bool isFullyAnswered = await _screeningService.IsSessionCompleteAsync(sessionId);
                if (!isFullyAnswered)
                {
                    TempData["ErrorMessage"] = "يرجى إكمال جميع الأسئلة قبل إنهاء الفحص.";
                    return RedirectToAction("Tool", new { clientId, sessionId, currentSectionIndex });
                }

                await _screeningService.ProcessFinalRecommendationAsync(sessionId);
                // (BE-01 — إصلاح): لم نعُد نمرّر التوصية عبر الرابط؛ صفحة Result تقرأها الآن مباشرة
                // من قاعدة البيانات (session.Recommendation) فلا حاجة لتمريرها هنا إطلاقًا.
                return RedirectToAction("Result", new { sessionId });
            }

            return RedirectToAction("Tool", new { clientId, sessionId, currentSectionIndex = currentSectionIndex + 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadDocument(int sessionId, int sectionId, IFormFile document)
        {
            var clientId = await _screeningService.GetClientIdForSessionAsync(sessionId);
            if (clientId == null) return Json(new { success = false, message = "الجلسة غير موجودة." });
            if (!await IsAuthorizedForClientAsync(clientId.Value))
            {
                return Json(new { success = false, message = "ليست لديك صلاحية الوصول لهذه الجلسة." });
            }

            // (SEC-09 — إصلاح): لم يكن هناك أي تحقق من نوع أو حجم الملف، بنفس مشكلة رفع مستندات
            // الهوية في ClientsController (موديل 2) — نعيد استخدام نفس المدقق المشترك هنا.
            if (!await _screeningService.IsSessionEditableAsync(sessionId)) return Conflict();
            var validation = await FileUploadValidator.ValidateAsync(document);
            if (!validation.IsValid)
            {
                return Json(new { success = false, message = validation.CombinedErrorMessage });
            }

            try
            {
                var sections = await _screeningService.GetSectionsAndQuestionsAsync();
                if (!sections.Any(s => s.Id == sectionId)) return BadRequest();
                string key = await _documents.SaveAsync("screening_docs", document);
                var saved = await _screeningService.SaveDocumentAsync(sessionId, sectionId, Path.GetFileName(document.FileName), key);
                if (saved == null) { _documents.Delete(key); return BadRequest(); }
                return Json(new { success = true, docId = saved.Id, fileName = saved.FileName,
                    path = Url.Action("Screening", "Documents", new { documentId = saved.Id }) });
            }
            catch (IOException ex)
            {
                // (ARCH-05): رسالة عامة آمنة للمستخدم بدل تسريب ex.Message الخام، مع تسجيل التفاصيل.
                _logger.LogError(ex, "فشل حفظ مستند فحص للجلسة {SessionId}", sessionId);
                return Json(new { success = false, message = "تعذّر حفظ الملف، حاول مرة أخرى." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int documentId)
        {
            if (documentId <= 0) return Json(new { success = false, message = "معرف الوثيقة غير صحيح" });

            var clientId = await _screeningService.GetClientIdForDocumentAsync(documentId);
            if (clientId == null) return Json(new { success = false, message = "الوثيقة غير موجودة." });
            if (!await IsAuthorizedForClientAsync(clientId.Value))
            {
                return Json(new { success = false, message = "ليست لديك صلاحية حذف هذه الوثيقة." });
            }

            var document = await _screeningService.GetDocumentAsync(documentId);
            if (document == null) return NotFound();
            if (!await _screeningService.IsSessionEditableAsync(document.SessionId)) return Conflict();
            bool isDeleted = await _screeningService.DeleteDocumentAsync(documentId);
            if (isDeleted) _documents.Delete(document?.FilePath);

            if (isDeleted)
            {
                return Json(new { success = true, message = "تم حذف الوثيقة بنجاح" });
            }

            return Json(new { success = false, message = "فشل حذف الوثيقة أو أنها غير موجودة" });
        }

        [HttpGet]
        public async Task<IActionResult> Result(int sessionId)
        {
            var clientId = await _screeningService.GetClientIdForSessionAsync(sessionId);
            if (clientId == null) return NotFound();
            if (!await IsAuthorizedForClientAsync(clientId.Value)) return Forbid();

            // (BE-01): لم يعُد Action يقبل recommendation من الرابط إطلاقًا.
            var model = await _screeningService.GetEvaluationResultViewModelAsync(sessionId);
            if (model == null) return NotFound();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SessionReport(int sessionId)
        {
            var clientId = await _screeningService.GetClientIdForSessionAsync(sessionId);
            if (clientId == null) return NotFound();
            if (!await IsAuthorizedForClientAsync(clientId.Value)) return Forbid();

            var session = await _screeningService.GetSessionReportAsync(sessionId);
            if (session == null) return NotFound();

            ViewBag.HasLoan = await _screeningService.HasLinkedLoanAsync(sessionId);

            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSession(int sessionId, int clientId)
        {
            // (تشديد إضافي، نفس نمط Tool/ProcessSection): نتحقق من العميل الحقيقي المرتبط
            // بالجلسة بدل الوثوق بـ clientId المُمرَّر في النموذج، لأن هذا إجراء حذف نهائي.
            var actualClientId = await _screeningService.GetClientIdForSessionAsync(sessionId);
            if (actualClientId == null) return NotFound();
            if (!await IsAuthorizedForClientAsync(actualClientId.Value)) return Forbid();

            if (!await _screeningService.DeleteSessionAsync(sessionId)) return Conflict();
            return RedirectToAction("Details", "Clients", new { clientId = actualClientId.Value });
        }
    }
}
