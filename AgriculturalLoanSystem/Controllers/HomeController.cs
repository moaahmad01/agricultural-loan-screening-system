using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace AgriculturalLoanSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppDbContext _context;

        public HomeController(UserManager<IdentityUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return View();
            }
            if (User.IsInRole("Citizen"))
            {
                var userId = _userManager.GetUserId(User);
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);

                if (client != null)
                {
                    return RedirectToAction("Details", "Clients", new { clientId = client.Id });
                }
                else
                {
                    return RedirectToAction("EditProfile", "Clients");
                }
            }
            if (User.IsInRole("Admin") || User.IsInRole("Employee"))
            {
                return RedirectToAction("Search", "Clients");
            }

            return RedirectToAction("Login", "Account");
        }

        public IActionResult SetLanguage(string culture, string? returnUrl = null)
        {
            if (culture is not ("ar-JO" or "en-US")) return BadRequest();
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            // إصلاح Open Redirect (SEC-10): لا نثق بقيمة Referer كوجهة توجيه إلا إذا كانت رابطًا محليًا
            // فعليًا ضمن هذا الموقع، وإلا نعود لصفحة رئيسية آمنة بدل تمرير أي رابط خارجي كما كان سابقًا.

            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                return RedirectToAction("Index", "Home");
            }

            return Redirect(returnUrl);
        }

        // Action كانت مفقودة رغم أن Program.cs يوجّه إليها كل الأخطاء غير المُعالَجة في بيئة الإنتاج
        // (UseExceptionHandler("/Home/Error")) ورغم وجود View جاهز (Views/Shared/Error.cshtml) بلا Action يخدمه.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}