using AgriculturalLoanSystem.Models.ViewModels;
using AgriculturalLoanSystem.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgriculturalLoanSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IClientsService _clientsService;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IClientsService clientsService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _clientsService = clientsService;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (await _clientsService.NationalIdExistsAsync(model.NationalId))
                {
                    ModelState.AddModelError("", "Unable to register. Contact staff if you already have a client record. / تعذر التسجيل، يرجى التواصل مع الموظف.");
                    return View(model);
                }

                var user = new IdentityUser { UserName = model.NationalId, PhoneNumber = model.PhoneNumber };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    try
                    {
                        var roleResult = await _userManager.AddToRoleAsync(user, "Citizen");
                        if (!roleResult.Succeeded) throw new InvalidOperationException("Citizen role assignment failed.");
                        await _clientsService.LinkOrCreateClientForNewUserAsync(
                            model.NationalId, model.FullNameAr, model.PhoneNumber, user.Id);
                    }
                    catch
                    {
                        await _userManager.DeleteAsync(user);
                        ModelState.AddModelError("", "Registration could not be completed. Please contact staff.");
                        return View(model);
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);

                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // (SEC-07 / موديل 1، بند 1.1): كان lockoutOnFailure=false، أي بلا أي حد لعدد
                // محاولات تخمين كلمة المرور الفاشلة لنفس الحساب. تفعيله الآن يعتمد على سياسة القفل
                // العامة المُهيَّأة مسبقًا في Program.cs ضمن المرحلة أ (5 محاولات / قفل 5 دقائق).
                var result = await _signInManager.PasswordSignInAsync(
                    model.NationalId, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    ModelState.AddModelError("",
                        "تم قفل الحساب مؤقتًا بسبب تكرار محاولات دخول فاشلة. حاول مرة أخرى بعد بضع دقائق. / " +
                        "This account has been temporarily locked due to repeated failed login attempts. Please try again in a few minutes.");
                }
                else
                {
                    ModelState.AddModelError("", "محاولة دخول غير صحيحة / Invalid login attempt");
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // كانت مفقودة (SEC-11) رغم أن Program.cs يحدّدها كـ AccessDeniedPath، ما كان يعني أن أي
        // مستخدم يُرفض وصوله (Forbid) يصطدم بصفحة 404 بدل رسالة واضحة "ليست لديك صلاحية".
        [HttpGet]
        public IActionResult AccessDenied() => View();
    }
}
