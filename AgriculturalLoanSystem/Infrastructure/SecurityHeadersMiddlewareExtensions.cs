namespace AgriculturalLoanSystem.Infrastructure
{
    /// <summary>
    /// رؤوس أمان HTTP أساسية (A.4 من خطة التصحيح — راجع SEC-14 في تقرير المراجعة).
    ///
    /// تحديث موديل 6: بعد نقل Tailwind/FontAwesome/Cairo/SweetAlert2 لاستضافة محلية بالكامل
    /// (FE-01/SEC-13)، أُزيلت كل نطاقات CDN الخارجية (cdn.tailwindcss.com، cdnjs.cloudflare.com،
    /// fonts.googleapis.com، fonts.gstatic.com، cdn.jsdelivr.net) من script-src/style-src/font-src
    /// نهائيًا — لم تعد هناك حاجة للسماح بها، وإبقاؤها كان سيوسّع سطح الهجوم بلا أي فائدة فعلية.
    ///
    /// 'unsafe-inline' لا يزال ضروريًا مؤقتًا في script-src وstyle-src، لأن جافاسكربت وتنسيقات
    /// Inline لا تزال موجودة ضمن عدة صفحات (خصوصًا معالج خطوات الفحص بـ Views/Screening/Tool.cshtml
    /// الأكبر حجمًا والأكثر ارتباطًا بقيم Razor مُحقَنة، لم يُستخرَج بالكامل بعد لملف JS منفصل ضمن
    /// موديل 6 — قرار مقصود لتفادي مخاطرة إعادة هيكلة أكبر بلا إمكانية اختبار تشغيلي فعلي). الخطوة
    /// التالية الفعلية لتشديد هذا أكثر: استخراج بقية الجافاسكربت Inline المتبقي ثم اعتماد Nonce/Hash
    /// لكل سكربت/تنسيق بدل 'unsafe-inline' كليًا.
    /// </summary>
    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                var headers = context.Response.Headers;

                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";

                headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline'; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "font-src 'self'; " +
                    "img-src 'self' data:; " +
                    "connect-src 'self'; " +
                    "object-src 'none'; " +
                    "frame-ancestors 'self'; " +
                    "base-uri 'self'; " +
                    "form-action 'self';";

                await next();
            });
        }
    }
}
