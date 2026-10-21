using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text.Json;

namespace AgriculturalLoanSystem.Infrastructure
{
    /// <summary>
    /// معالج استثناءات عام (A.9 من خطة التصحيح — راجع ARCH-05 في تقرير المراجعة):
    /// يمنع تسريب تفاصيل الاستثناء الخام (ex.Message / Stack Trace) للمستخدم النهائي عبر
    /// استجابات AJAX/JSON (مثل نقاط النهاية في ScreeningController)، ويسجّل كل استثناء غير
    /// معالَج مركزيًا بدل ما كان يحدث سابقًا من تسجيل جزئي ومتفرّق داخل كل Controller على حدة.
    ///
    /// لطلبات الصفحات العادية (HTML) نُرجع false فنترك المعالجة لآلية
    /// app.UseExceptionHandler("/Home/Error") الموجودة أصلًا في Program.cs.
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(
                exception,
                "استثناء غير معالَج أثناء معالجة الطلب {Method} {Path} (TraceId={TraceId})",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);

            bool wantsJson =
                string.Equals(httpContext.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
                httpContext.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

            if (!wantsJson)
            {
                // نترك المعالجة لصفحة /Home/Error الخاصة بطلبات المتصفح العادية
                return false;
            }

            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            httpContext.Response.ContentType = "application/json; charset=utf-8";

            bool isArabic = System.Threading.Thread.CurrentThread.CurrentCulture.Name.StartsWith("ar");
            var payload = new
            {
                success = false,
                message = isArabic
                    ? "حدث خطأ غير متوقع في الخادم. تم إبلاغ الفريق التقني، الرجاء المحاولة لاحقًا."
                    : "An unexpected server error occurred. The technical team has been notified.",
                traceId = httpContext.TraceIdentifier
            };

            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload), cancellationToken);
            return true;
        }
    }
}
