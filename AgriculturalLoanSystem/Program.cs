using Microsoft.AspNetCore.DataProtection;
using AgriculturalLoanSystem.Data;
using AgriculturalLoanSystem.Data.Interceptors;
using AgriculturalLoanSystem.Infrastructure;
using AgriculturalLoanSystem.Services.Implementations;
using AgriculturalLoanSystem.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDataProtection().PersistKeysToFileSystem(
    new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys")));

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddSingleton<PrivateDocumentStore>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IScreeningService, ScreeningService>();
builder.Services.AddScoped<ILoanCalculationService, LoanCalculationService>();
builder.Services.AddScoped<IClientsService, ClientsService>(); // موديل 1: خدمة العملاء الجديدة
builder.Services.AddScoped<ILoansService, LoansService>(); // موديل 4: خدمة القروض الجديدة
// (A.6) أُزيل تسجيل DI المكرر لـ ILoanCalculationService وIScreeningService الذي كان مكررًا سطرين لاحقًا

// (A.7) سلسلة الاتصال تُقرأ من IConfiguration، والذي يقرأ تلقائيًا من appsettings.json ثم
// يسمح بالتجاوز عبر متغير بيئة قياسي باسم ConnectionStrings__DefaultConnection (بدون أي كود إضافي)،
// وهذا يحل غياب قابلية النقل بين البيئات دون الحاجة لتخزين أي سر داخل الكود المصدري.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(connectionString);

    var interceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
    options.AddInterceptors(interceptor);
});

builder.Services.AddHttpContextAccessor();
// (موديل 7 — ج.6): تخزين مؤقت في الذاكرة لبيانات شبه ثابتة (أقسام/أسئلة الفحص، معدلات الفائدة)
builder.Services.AddMemoryCache();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // (A.2) سياسة كلمة مرور أقوى — كانت سابقًا 6 أحرف بلا أي اشتراط تعقيد (SEC-06)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    options.User.RequireUniqueEmail = false;

    // (A.3) تفعيل قفل الحساب افتراضيًا بعد عدد محاولات فاشلة (الاستدعاء الفعلي lockoutOnFailure:true
    // في AccountController.Login يصير لاحقًا ضمن موديل 1؛ هذه الإعدادات تجهّز السياسة العامة الآن)
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// (A.9) معالج استثناءات عام مركزي — يمنع تسريب تفاصيل الأخطاء الخام في استجابات AJAX/JSON
// (راجع Infrastructure/GlobalExceptionHandler.cs وARCH-05 في التقرير)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// (A.5) Rate Limiting أساسي على مسارات تسجيل الدخول والتسجيل تحديدًا (SEC-15)
// سياسة نافذة ثابتة: 10 محاولات كل دقيقة لكل عنوان IP، مع طابور صغير بدل الرفض الفوري الخشن
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

var supportedCultures = new[] { "ar-JO", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // (A.8) فصل استراتيجية الترحيل عن الإقلاع التلقائي: تشغيل Migrate() تلقائيًا كل مرة
        // يقلع فيها التطبيق مناسب فقط لبيئة التطوير المحلية. في بيئة حقيقية (Staging/Production)
        // يجب أن يكون الترحيل خطوة نشر صريحة ومنفصلة (dotnet ef database update) لتفادي تسابق
        // عدة نسخ من التطبيق على ترحيل نفس القاعدة في آنٍ واحد (راجع DEVOPS-03 في التقرير).
        if (app.Environment.IsDevelopment())
        {
            var context = services.GetRequiredService<AppDbContext>();
            context.Database.Migrate();
        }

        await SeedData.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database initialization failed; startup aborted.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders(); // (A.4)
// Block legacy upload URLs before either static-file implementation can serve them.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter(); // (A.5)

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
  name: "default",
  pattern: "{controller=Home}/{action=Index}/{id?}")
  .WithStaticAssets();

app.Run();
