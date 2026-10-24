using AgriculturalLoanSystem.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using AgriculturalLoanSystem.Services.Interfaces;
using AgriculturalLoanSystem.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;

namespace AgriculturalLoanSystem.Controllers
{
    // خطوة صفرية فورية: حماية خشنة لكامل لوحة الإدارة إلى حين تفصيل الصلاحيات لكل إجراء (موديل 5)
    [Authorize(Roles = "Admin,Employee")]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            IAdminService adminService,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _adminService = adminService;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        private bool IsArabic => CultureInfo.CurrentCulture.Name.StartsWith("ar");

        [HttpGet]
        public async Task<IActionResult> ManageUsers()
        {
            var usersData = await _adminService.GetUsersWithClientDataAsync();

            // (CQ-05 — إصلاح N+1): استعلام واحد بدل حلقة FindByIdAsync/GetRolesAsync لكل مستخدم —
            // كانت تعني رحلتين إضافيتين لقاعدة البيانات لكل مستخدم معروض بهذه الصفحة.
            var userRoles = await _adminService.GetUserRolesMapAsync();

            int employeeCount = 0, citizenCount = 0;
            foreach (var item in usersData)
            {
                if (userRoles.TryGetValue(item.UserId, out var roles))
                {
                    if (roles.Contains("Employee")) employeeCount++;
                    else citizenCount++;
                }
            }

            ViewBag.UserRoles = userRoles;
            ViewBag.EmployeeCount = employeeCount;
            ViewBag.CitizenCount = citizenCount;

            return View(usersData);
        }

        [HttpGet]
        public async Task<IActionResult> Sections()
        {
            var sections = await _adminService.GetAllSectionsAsync();
            return View(sections);
        }

        [HttpGet]
        public async Task<IActionResult> CreateSection()
        {
            var sections = await _adminService.GetAllSectionsAsync();
            int nextOrder = sections.Any() ? sections.Max(s => s.DisplayOrder) + 1 : 1;

            int currentTotalWeight = sections.Sum(s => s.Weight);
            int remainingWeight = 100 - currentTotalWeight;

            ViewBag.SuggestedOrder = nextOrder;
            ViewBag.RemainingWeight = remainingWeight;
            ViewBag.CurrentTotalWeight = currentTotalWeight;

            return View(new Section { DisplayOrder = nextOrder, IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSection(
            [Bind("SectionNameAr,SectionNameEn,Weight,DisplayOrder,IsActive")] Section section)
        {
            var sections = await _adminService.GetAllSectionsAsync();

            // (DB-04): الفحص القديم كان يقارن مقابل كل الأقسام بما فيها المعطَّلة منطقيًا
            // (IsActive=false)، ما كان يمنع إعادة استخدام ترتيب قسم مُعطَّل لقسم جديد فعّال.
            var activeSections = sections.Where(s => s.IsActive).ToList();
            if (activeSections.Any(s => s.DisplayOrder == section.DisplayOrder))
            {
                ModelState.AddModelError("DisplayOrder", "هذا الترتيب مستخدم بالفعل، يرجى اختيار رقم آخر.");
            }

            int currentTotalWeight = activeSections.Sum(s => s.Weight);
            if (currentTotalWeight + section.Weight > 100)
            {
                ModelState.AddModelError("Weight", $"لا يمكن إضافة هذا الوزن. المجموع سيصبح {currentTotalWeight + section.Weight}% وهو يتجاوز 100%. المتاح حالياً: {100 - currentTotalWeight}%.");
            }

            if (ModelState.IsValid)
            {
                await _adminService.CreateSectionAsync(section);
                return RedirectToAction(nameof(Sections));
            }

            ViewBag.SuggestedOrder = section.DisplayOrder; ViewBag.RemainingWeight = 100 - currentTotalWeight;
            ViewBag.CurrentTotalWeight = currentTotalWeight;

            return View(section);
        }

        [HttpGet]
        public async Task<IActionResult> EditSection(int id)
        {
            var section = await _adminService.GetSectionByIdAsync(id);
            if (section == null) return NotFound();

            var allSections = await _adminService.GetAllSectionsAsync();

            int otherSectionsWeight = allSections.Where(s => s.Id != id).Sum(s => s.Weight);

            int maxAllowedForThisSection = 100 - otherSectionsWeight;

            ViewBag.RemainingWeight = maxAllowedForThisSection; ViewBag.CurrentTotalWeight = otherSectionsWeight;
            return View(section);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSection(Section section)
        {
            var allSections = await _adminService.GetAllSectionsAsync();
            var otherSections = allSections.Where(s => s.Id != section.Id && s.IsActive).ToList();

            if (otherSections.Any(s => s.DisplayOrder == section.DisplayOrder))
            {
                ModelState.AddModelError("DisplayOrder", "هذا الترتيب مستخدم في قسم آخر.");
            }

            int otherSectionsWeight = otherSections.Sum(s => s.Weight);
            if (otherSectionsWeight + section.Weight > 100)
            {
                ModelState.AddModelError("Weight", $"المجموع سيتجاوز 100%. المتاح لهذا القسم كحد أقصى هو {100 - otherSectionsWeight}%.");
            }

            if (ModelState.IsValid)
            {
                await _adminService.UpdateSectionAsync(section);
                return RedirectToAction(nameof(Sections));
            }

            ViewBag.RemainingWeight = 100 - otherSectionsWeight;
            ViewBag.CurrentTotalWeight = otherSectionsWeight;

            return View(section);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSection(int id)
        {
            var (success, wasSoftDeactivated) = await _adminService.DeleteSectionAsync(id);

            if (!success)
            {
                TempData["ErrorMessage"] = IsArabic ? "القسم غير موجود." : "Section not found.";
            }
            else if (wasSoftDeactivated)
            {
                // (DB-02): لم يُحذف فعليًا لوجود أسئلة مرتبطة به — عُطِّل منطقيًا فقط.
                TempData["InfoMessage"] = IsArabic
                    ? "لا يمكن حذف هذا القسم نهائيًا لوجود أسئلة مرتبطة به وقد تكون مستخدمة في تقييمات سابقة، لذلك تم تعطيله بدلاً من ذلك ولن يظهر في الاستبيانات الجديدة."
                    : "This section can't be permanently deleted because it has related questions that may be used in past evaluations, so it was deactivated instead and won't appear in new questionnaires.";
            }
            else
            {
                TempData["InfoMessage"] = IsArabic ? "تم حذف القسم بنجاح." : "Section deleted successfully.";
            }

            return RedirectToAction(nameof(Sections));
        }
        [HttpGet]
        public async Task<IActionResult> Questions(int? sectionId)
        {
            var questions = await _adminService.GetQuestionsAsync(sectionId);
            ViewBag.Sections = new SelectList(await _adminService.GetAllSectionsAsync(), "Id", "SectionNameAr");
            ViewBag.SelectedSectionId = sectionId;
            return View(questions);
        }

        [HttpGet]
        public async Task<IActionResult> CreateQuestion()
        {
            ViewBag.SectionId = new SelectList(await _adminService.GetAllSectionsAsync(), "Id", "SectionNameAr");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestion(
            [Bind("SectionId,QuestionTextAr,QuestionTextEn,PointValue,DisplayOrder,IsActive")] Question question)

        {
            if (question.PointValue < 1 || question.PointValue > 5)
            {
                ModelState.AddModelError("ValuePoints", IsArabic
                    ? "يجب أن تكون قيمة الأهمية بين 1 و 5 فقط."
                    : "Value points must be between 1 and 5.");
            }
            if (question.SectionId <= 0 || await _adminService.GetSectionByIdAsync(question.SectionId) == null)
            {
                ModelState.AddModelError("SectionId", IsArabic ? "يرجى اختيار القسم الذي ينتمي إليه السؤال." : "Please select the section for this question.");
            }

            var questionsInSection = await _adminService.GetQuestionsAsync(question.SectionId);
            if (questionsInSection.Any(q => q.IsActive && q.DisplayOrder == question.DisplayOrder))
            {
                ModelState.AddModelError("DisplayOrder", IsArabic
                    ? $"هذا الترتيب ({question.DisplayOrder}) مستخدم بالفعل في هذا القسم، يرجى اختيار رقم آخر."
                    : $"Order {question.DisplayOrder} is already used in this section.");
            }

            if (ModelState.IsValid)
            {
                await _adminService.CreateQuestionAsync(question);
                return RedirectToAction(nameof(Questions), new { sectionId = question.SectionId });
            }

            var sections = await _adminService.GetAllSectionsAsync();
            ViewBag.SectionId = new SelectList(sections, "Id", IsArabic ? "SectionNameAr" : "SectionNameEn", question.SectionId);

            return View(question);
        }

        [HttpGet]
        public async Task<IActionResult> EditQuestion(int id)
        {
            var question = await _adminService.GetQuestionByIdAsync(id);
            if (question == null) return NotFound();

            ViewBag.SectionId = new SelectList(await _adminService.GetAllSectionsAsync(), "Id", "SectionNameAr", question.SectionId);
            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestion(Question question)
        {
            if (question.PointValue < 1 || question.PointValue > 5)
            {
                ModelState.AddModelError("PointValue", IsArabic
                    ? "يجب أن تكون قيمة الأهمية بين 1 و 5 فقط."
                    : "Value points must be between 1 and 5.");
            }

            if (question.SectionId <= 0)
            {
                ModelState.AddModelError("SectionId", IsArabic
                    ? "يرجى اختيار القسم الذي ينتمي إليه السؤال."
                    : "Please select the section for this question.");
            }

            var questionsInSection = await _adminService.GetQuestionsAsync(question.SectionId);
            if (questionsInSection.Any(q => q.IsActive && q.DisplayOrder == question.DisplayOrder && q.Id != question.Id))
            {
                ModelState.AddModelError("DisplayOrder", IsArabic
                    ? $"هذا الترتيب ({question.DisplayOrder}) مستخدم بالفعل في هذا القسم، يرجى اختيار رقم آخر."
                    : $"Order {question.DisplayOrder} is already used in this section.");
            }

            if (ModelState.IsValid)
            {
                await _adminService.UpdateQuestionAsync(question);
                return RedirectToAction(nameof(Questions), new { sectionId = question.SectionId });
            }

            // في حال وجود خطأ، نعيد ملء القائمة المنسدلة للأقسام لإظهارها في الـ View
            var sections = await _adminService.GetAllSectionsAsync();
            ViewBag.SectionId = new SelectList(sections, "Id", IsArabic ? "SectionNameAr" : "SectionNameEn", question.SectionId);

            return View(question);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var question = await _adminService.GetQuestionByIdAsync(id);
            int? sectionId = question?.SectionId;

            if (question != null)
            {
                var (success, wasSoftDeactivated) = await _adminService.DeleteQuestionAsync(id);

                if (wasSoftDeactivated)
                {
                    // (DB-02): لم يُحذف فعليًا لوجود إجابات فحص مرتبطة به — عُطِّل منطقيًا فقط.
                    TempData["InfoMessage"] = IsArabic
                        ? "لا يمكن حذف هذا السؤال نهائيًا لوجود إجابات فحص مرتبطة به قد تكون جزءًا من تقييمات سابقة، لذلك تم تعطيله بدلاً من ذلك."
                        : "This question can't be permanently deleted because it has related screening answers that may be part of past evaluations, so it was deactivated instead.";
                }
                else if (success)
                {
                    TempData["InfoMessage"] = IsArabic
                        ? "تم حذف السؤال بنجاح."
                        : "Question deleted successfully.";
                }
            }

            return RedirectToAction(nameof(Questions), new { sectionId = sectionId });
        }

        [HttpGet]
        public async Task<IActionResult> PendingLoans(int page = 1)
        {
            const int pageSize = 20;
            var (items, total) = await _adminService.GetPendingLoansAsync(page, pageSize);

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = total;
            ViewBag.TotalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLoan(int loanId)
        {
            var success = await _adminService.ApproveLoanAsync(loanId);
            if (success) TempData["SuccessMessage"] = IsArabic ? "تم الاعتماد." : "Approved.";
            return RedirectToAction(nameof(PendingLoans));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLoan(int loanId)
        {
            var success = await _adminService.RejectLoanAsync(loanId);
            if (success) TempData["ErrorMessage"] = IsArabic ? "تم الرفض." : "Rejected.";
            return RedirectToAction(nameof(PendingLoans));
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var stats = await _adminService.GetDashboardStatsAsync();
            return View(stats);
        }

        [HttpGet]
        public async Task<IActionResult> AuditLogs(
            int page = 1,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? userId = null,
            string? entityName = null)
        {
            const int pageSize = 50;

            // (DB-07 — إصلاح): كانت هذه الصفحة تعرض دومًا آخر 100 سجل فقط بلا أي طريقة لتصفّح
            // ما هو أقدم أو فلترته — عمليًا تفقد فائدتها كسجل تدقيق فور تجاوز النظام 100 عملية حفظ.
            var (items, totalCount) = await _adminService.GetAuditLogsAsync(
                page, pageSize, fromDate, toDate, userId, entityName);

            var viewModel = new AuditLogsViewModel
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                FromDate = fromDate,
                ToDate = toDate,
                UserId = userId,
                EntityName = entityName
            };

            return View(viewModel);
        }

        // (SEC-01 — التفصيل النهائي): هذا الإجراء تحديدًا يمنح صلاحيات موظف (وصول للوحة الإدارة
        // بأكملها)، لذلك يُحصَر بدور Admin حصرًا — بخلاف بقية أكشنز هذا الكونترولر المتاحة أيضًا
        // لـ Employee (التي تبقى محكومة بحماية الكلاس العامة Admin,Employee من الخطوة الصفرية).
        // بدون هذا التقييد، أي موظف عادي كان يقدر يُرقّي أي مستخدم آخر (أو نفسه لاحقًا) لدور موظف.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MakeEmployee(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (!await _roleManager.RoleExistsAsync("Employee"))
                await _roleManager.CreateAsync(new IdentityRole("Employee"));

            await _userManager.RemoveFromRoleAsync(user, "Citizen");
            await _userManager.AddToRoleAsync(user, "Employee");

            return RedirectToAction(nameof(ManageUsers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveEmployee(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            await _userManager.RemoveFromRoleAsync(user, "Employee");
            if (!await _roleManager.RoleExistsAsync("Citizen"))
                await _roleManager.CreateAsync(new IdentityRole("Citizen"));

            await _userManager.AddToRoleAsync(user, "Citizen");
            return RedirectToAction(nameof(ManageUsers));
        }
    }
}
