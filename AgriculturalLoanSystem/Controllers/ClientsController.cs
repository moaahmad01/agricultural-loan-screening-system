using AgriculturalLoanSystem.Data.Entities;
using AgriculturalLoanSystem.Infrastructure;
using AgriculturalLoanSystem.Models.ViewModels;
using AgriculturalLoanSystem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AgriculturalLoanSystem.Controllers
{
    // (اكتشاف إضافي أثناء موديل 2): هذا الكلاس لم يكن ضمن أي namespace إطلاقًا في الكود الأصلي
    // (كان في الـ Global Namespace) بخلاف كل الكونترولرز الأخرى — تم توحيده هنا مع البقية.
    [Authorize]
    public class ClientsController : Controller
    {
        private readonly IClientsService _clientsService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly PrivateDocumentStore _documents;
        private readonly ILogger<ClientsController> _logger;

        public ClientsController(
            IClientsService clientsService,
            UserManager<IdentityUser> userManager,
            PrivateDocumentStore documents,
            ILogger<ClientsController> logger)
        {
            _clientsService = clientsService;
            _userManager = userManager;
            _documents = documents;
            _logger = logger;
        }

        // (CQ-03): ميزة إضافة عميل كانت غير مكتملة أصلاً — Action بلا View وبلا معالج POST.
        // أُكملت هنا بالكامل. الوصول محصور بالموظفين/الأدمن فقط: العميل نفسه يحصل على سجله
        // تلقائيًا عبر IClientsService.LinkOrCreateClientForNewUserAsync أثناء التسجيل (موديل 1)،
        // فلا داعٍ ولا يجوز أن يُنشئ عميل عاديّ سجلات عملاء أخرى بنفسه.
        [HttpGet]
        [Authorize(Roles = "Admin,Employee")]
        public IActionResult Create()
        {
            return View(new ClientCreateViewModel
            {
                DateOfBirth = DateOnly.FromDateTime(DateTime.Now.AddYears(-50))
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> Create(ClientCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (await _clientsService.NationalIdExistsAsync(model.NationalId))
                {
                    ModelState.AddModelError(
                        nameof(model.NationalId),
                        "يوجد عميل مسجَّل بهذا الرقم الوطني مسبقًا / A client with this National ID already exists");
                }
                else
                {
                    var client = await _clientsService.CreateClientAsync(model);
                    return RedirectToAction(nameof(Details), new { clientId = client.Id });
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile(int clientId)
        {
            var userId = _userManager.GetUserId(User);
            var client = await _clientsService.GetClientEntityAsync(clientId);

            if (client == null) return NotFound();

            if (!client.DateOfBirth.HasValue || client.DateOfBirth.Value.Year <= 1)
            {
                client.DateOfBirth = DateOnly.FromDateTime(DateTime.Now.AddYears(-50));
            }

            bool isStaff = User.IsInRole("Admin") || User.IsInRole("Employee");
            bool isOwner = client.ApplicationUserId == userId;

            if (!isStaff && !isOwner) return Forbid();

            return View(client);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> EditProfile(
            int id,
            [Bind("Id,FullNameAr,FullNameEn,PhoneNumber,DateOfBirth,Region,DetailedArea,Address,NationalId,ApplicationUserId,CreatedAt")] Client client,
            IFormFile? identityFile)
        {
            if (id != client.Id) return NotFound();

            // (SEC-08 — إصلاح حرج): نسخة GET من هذا الإجراء كانت تتحقق من isStaff/isOwner، لكن
            // نسخة POST (التي تُنفِّذ التعديل الفعلي) لم تكن تتحقق من أي شيء — ما كان يسمح لأي
            // مستخدم Citizen مسجَّل دخول بتعديل بيانات أي عميل آخر عبر إرسال id مختلف مباشرة.
            var existingClientCheck = await _clientsService.GetClientEntityAsync(id);
            if (existingClientCheck == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isStaff = User.IsInRole("Admin") || User.IsInRole("Employee");
            bool isOwner = existingClientCheck.ApplicationUserId == userId;
            if (!isStaff && !isOwner) return Forbid();

            ModelState.Remove("ApplicationUser");
            ModelState.Remove("PreviousLoans");
            ModelState.Remove("LoanCalculations");
            ModelState.Remove("ScreeningSessions");

            if (!ModelState.IsValid)
            {
                return View(client);
            }

            var oldIdentityDocumentPath = existingClientCheck.IdentityDocumentPath;
            string? newIdentityDocumentPath = null;

            if (identityFile != null && identityFile.Length > 0)
            {
                // (SEC-09): لم يكن هناك أي تحقق من نوع أو حجم الملف — أي ملف بأي امتداد وبأي حجم
                // كان يُقبل ويُحفظ مباشرة على القرص.
                var validation = await FileUploadValidator.ValidateAsync(identityFile);
                if (!validation.IsValid)
                {
                    ModelState.AddModelError(nameof(identityFile), validation.CombinedErrorMessage);
                    return View(client);
                }

                try
                {
                    newIdentityDocumentPath = await _documents.SaveAsync("identity_docs", identityFile);
                }
                catch (IOException ex)
                {
                    // (ARCH-05): لم نعُد نُظهر ex.Message الخام للمستخدم — يُسجَّل بالتفصيل في الـ
                    // Log لفريق الدعم، بينما يرى المستخدم رسالة عامة آمنة فقط.
                    _logger.LogError(ex, "فشل حفظ ملف مستند الهوية للعميل {ClientId}", id);
                    ModelState.AddModelError("", "تعذّر حفظ الملف المرفوع، حاول مرة أخرى / Failed to save the uploaded file, please try again");
                    return View(client);
                }
            }

            var updated = await _clientsService.UpdateProfileAsync(
                id,
                client.FullNameAr,
                client.FullNameEn,
                client.PhoneNumber,
                client.DateOfBirth,
                client.Region,
                client.DetailedArea,
                client.Address,
                newIdentityDocumentPath);

            if (updated == null) return NotFound();
            // Delete the previous file only after the new reference has been persisted.
            if (newIdentityDocumentPath != null && oldIdentityDocumentPath != null)
                _documents.Delete(oldIdentityDocumentPath);

            return RedirectToAction(nameof(Details), new { clientId = id });
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] int page = 1)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Employee"))
            {
                // (CQ-02 — إصلاح): كان الكود يمرّر _userManager.GetUserId(User) (نص GUID خاص بحساب
                // Identity) مباشرة كـ clientId (رقم صحيح) لصفحة Details — ربط خاطئ للنوع كان
                // يكسر التوجيه دائمًا لكل مستخدم Citizen يفتح هذه الصفحة. الإصلاح: البحث فعليًا
                // عن سجل Client المرتبط بحساب المستخدم الحالي وتمرير معرّفه الصحيح (Client.Id).
                var userId = _userManager.GetUserId(User);
                var myClient = userId != null
                    ? await _clientsService.GetClientByApplicationUserIdAsync(userId)
                    : null;

                if (myClient != null)
                {
                    return RedirectToAction(nameof(Details), new { clientId = myClient.Id });
                }

                // المستخدم مسجَّل دخول لكن لا يملك سجل Client مرتبطًا بعد (حالة نادرة إن فشل
                // الربط التلقائي أثناء التسجيل) — نوجّهه لصفحة تواصل بدل صفحة معطّلة كما كان سابقًا.
                TempData["ErrorMessage"] = "لم يتم العثور على ملف عميل مرتبط بحسابك. تواصل مع الدعم الفني.";
                return RedirectToAction("Index", "Home");
            }

            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var employeeUsers = await _userManager.GetUsersInRoleAsync("Employee");

            var excludedUserIds = adminUsers.Select(u => u.Id)
                                           .Concat(employeeUsers.Select(u => u.Id))
                                           .ToList();

            const int pageSize = 20;
            var (items, total) = await _clientsService.SearchClientsAsync(query, excludedUserIds, page, pageSize);

            ViewBag.TotalClients = total;

            return View(new ClientSearchViewModel
            {
                SearchQuery = query ?? string.Empty,
                Clients = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int clientId)
        {
            var userId = _userManager.GetUserId(User);

            if (!User.IsInRole("Admin") && !User.IsInRole("Employee"))
            {
                var client = await _clientsService.GetClientEntityAsync(clientId);
                bool isOwner = client != null && client.ApplicationUserId == userId;
                if (!isOwner) return Forbid();
            }

            var viewModel = await _clientsService.GetDetailsAsync(clientId);
            if (viewModel == null) return NotFound();

            ViewBag.LoanStatusMap = await _clientsService.GetLoanStatusMapAsync(clientId);
            return View(viewModel);
        }
    }
}
