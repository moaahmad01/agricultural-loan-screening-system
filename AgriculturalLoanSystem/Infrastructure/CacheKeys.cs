namespace AgriculturalLoanSystem.Infrastructure
{
    /// <summary>
    /// مفاتيح IMemoryCache مركزية (موديل 7 — ج.6). بيانات الأقسام/الأسئلة/معدلات الفائدة شبه
    /// ثابتة (تتغيّر فقط حين يعدّلها الأدمن) لكنها تُقرأ بكثرة جدًا — كل عرض لصفحة أداة الفحص
    /// (Tool.cshtml) وكل عملية حساب قرض. تجميع المفاتيح هنا يمنع اختلاف الكتابة بين مكان
    /// الكتابة (ScreeningService/LoanCalculationService) ومكان الإبطال (AdminService).
    /// </summary>
    public static class CacheKeys
    {
        public const string ActiveSectionsWithQuestions = "cache:screening:active-sections-with-questions";
        public const string InterestRates = "cache:loans:interest-rates";
    }
}
