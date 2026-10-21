using AgriculturalLoanSystem.Controllers;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using Xunit;

namespace AgriculturalLoanSystem.Tests.Controllers
{
    /// <summary>
    /// اختبارات انحدار حرجة (موديل 7 — ج.4، أولوية 1): تضمن عدم إمكانية إزالة حماية [Authorize]
    /// عن أي من الكونترولرز الثلاثة التي كانت مكشوفة بالكامل بلا أي مصادقة قبل الإصلاح
    /// (SEC-01, SEC-02, SEC-03) دون أن يفشل اختبار آلي فورًا يفضح ذلك عند أي Pull Request مستقبلي.
    ///
    /// هذه أهم فئة اختبارات بكامل المشروع من ناحية القيمة/الجهد: لا تحتاج قاعدة بيانات ولا خادم
    /// فعلي، تعتمد فقط على فحص الـ Attributes عبر Reflection، وتفشل فورًا لو حذف أحدهم [Authorize]
    /// بالغلط أثناء أي تعديل مستقبلي.
    /// </summary>
    public class AuthorizationPresenceTests
    {
        [Theory]
        [InlineData(typeof(AdminController))]
        [InlineData(typeof(LoansController))]
        [InlineData(typeof(ScreeningController))]
        [InlineData(typeof(ClientsController))]
        [InlineData(typeof(DocumentsController))]
        public void Controller_MustHaveAuthorizeAttribute_AtClassLevel(Type controllerType)
        {
            var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Any();
            var hasAllowAnonymous = controllerType.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Any();

            Assert.True(hasAuthorize && !hasAllowAnonymous,
                $"{controllerType.Name} بلا [Authorize] على مستوى الكلاس — هذا بالضبط الثغرة " +
                $"الحرجة (SEC-01/02/03) التي أُصلحت سابقًا. إن كان هذا مقصودًا فعلاً، أضِف " +
                $"[AllowAnonymous] صراحة ليكون القرار موثَّقًا بالكود بدل غياب عرضي للحماية.");
        }

        [Fact]
        public void AdminController_MakeEmployee_MustBeRestrictedToAdminRoleOnly()
        {
            // (SEC-01 — التفصيل النهائي من موديل 5): هذا الإجراء يمنح صلاحيات موظف، ويجب أن يبقى
            // محصورًا بدور Admin حصرًا حتى لو تغيّرت حماية بقية الكونترولر مستقبلاً.
            var method = typeof(AdminController).GetMethod(nameof(AdminController.MakeEmployee));
            Assert.NotNull(method);

            var authorizeAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authorizeAttr);
            Assert.Equal("Admin", authorizeAttr!.Roles);
        }

        [Fact]
        public void AdminController_RemoveEmployee_MustBeRestrictedToAdminRoleOnly()
        {
            var method = typeof(AdminController).GetMethod(nameof(AdminController.RemoveEmployee));
            Assert.NotNull(method);

            var authorizeAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authorizeAttr);
            Assert.Equal("Admin", authorizeAttr!.Roles);
        }
    }
}
