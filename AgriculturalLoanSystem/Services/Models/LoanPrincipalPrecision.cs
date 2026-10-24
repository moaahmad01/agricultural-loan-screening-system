using System.ComponentModel.DataAnnotations;

namespace AgriculturalLoanSystem.Services.Models
{
    /// <summary>
    /// (FIN-01C — Option A، قرار المالك): رفض قيم أصل القرض غير القابلة للتمثيل
    /// بدقة نقدية منزلتين عشريتين (0.01 دينار) بدل تقريبها بصمت، لأن العمود
    /// decimal(12,2) في SQL Server يقرّب بصيغة "نصف بعيدًا عن الصفر" بينما جدول
    /// السداد يستخدم MidpointRounding.ToEven — ما يُنتج فرق تخزين بمقدار 0.01 دينار
    /// بين جدولا LoanCalculations و PaymentSchedules (حالة 1000.545 المؤكدة في FIN-01B-R/D5).
    ///
    /// المقارنة عددية بحتة (decimal) ولا تعتمد على أي ثقافة أو تنسيق نصي:
    /// القيمة مقبولة فقط إذا كانت تساوي تقريبها إلى منزلتين بـ MidpointRounding.ToEven.
    /// القيمة المُرسَلة لا تُعدَّل ولا تُقرَّب أبدًا (لا أي تسوية صامتة).
    ///
    /// التنفيذ مشترك في موضعين اثنين فقط (لا تكرار للقاعدة):
    /// 1) LoanPrincipalPrecisionAttribute على LoanInputModel.PrincipalAmount — مسار MVC:
    ///    يُنفَّذ ضمن ModelState.Validation قبل الحساب وقبل أي إدخال قاعدة بيانات.
    /// 2) حارس دفاعي في LoansService.CreateLoanApplicationAsync — أي نقطة حفظ داخلية
    ///    لا تمرّ عبر MVC تُرفض أيضًا قبل إضافة أي كيان أو حفظ.
    /// </summary>
    public static class LoanPrincipalPrecision
    {
        /// <summary>رسالة التحقق العربية الموحّدة (آمنة للعرض المباشر في MVC).</summary>
        public const string ErrorMessage =
            "قيمة القرض يجب أن تحتوي على منزلتين عشريتين كحد أقصى.";

        /// <summary>
        /// true فقط إذا كان المبلغ قابلًا للتمثيل بدقة منزلتين (مقبول: 1000، 1000.50،
        /// 1000.000؛ مرفوض: 1000.545، 1000.555، 1000.001). مقارنة عددية مستقلة عن الثقافة.
        /// </summary>
        public static bool IsRepresentableAtTwoDecimals(decimal amount)
            => amount == decimal.Round(amount, 2, MidpointRounding.ToEven);
    }

    /// <summary>
    /// (FIN-01C): DataAnnotation لدقة أصل القرض على مسار MVC — تُقيَّم ضمن ModelState
    /// تلقائيًا قبل تنفيذ أي كود في الإجراء، فلا يحدث حساب ولا إدخال لقيمة غير دقيقة.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class LoanPrincipalPrecisionAttribute : ValidationAttribute
    {
        public LoanPrincipalPrecisionAttribute() : base(LoanPrincipalPrecision.ErrorMessage)
        {
        }

        public override bool IsValid(object? value)
            => value is not decimal amount || LoanPrincipalPrecision.IsRepresentableAtTwoDecimals(amount);
    }
}
