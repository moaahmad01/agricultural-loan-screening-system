using AgriculturalLoanSystem.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace AgriculturalLoanSystem.Services.Models
{
    public class LoanInputModel
    {
        // (CQ-04 — إصلاح): [Required] على حقول قيمية (decimal/int) لا تأثير فعلي له (القيمة 0
        // تُعتبر "موجودة" دومًا)، فلم يكن هناك أي حد فعلي يمنع مدة صفر أو مبلغًا سالبًا/ضخمًا بلا حدود.
        // (FIN-01C — Option A): المرسَل نفسه يجب أن يكون قابلًا للتمثيل بدقة 0.01 دينار
        // (منزلتين عشريتين كحد أقصى). التحقق يُنفَّذ ضمن ModelState قبل الحساب وقبل الحفظ؛
        // تُرفض قيمة مثل 1000.545 برسالة MVC العربية دون أي تقريب صامت للقيمة المُرسَلة.
        // (VAL-01): الحَدان 1 دينار..10,000,000 دينار كالسابق تمامًا (شموليان)، لكن المقارنة
        // صارت عشرية صريحة (typeof(decimal)) بحدود ثابتة ثقافةً. كانت المقارنة السابقة تمرّ
        // عبر تحويل int (OperandType = Int32)، فتعبر النطاق قيم مثل 0.99
        // (تقريبًا إلى 1) و10,000,000.50 (تقريبًا إلى 10,000,000) رغم كونها خارج الحدين المقصودين.
        [Required]
        [Range(typeof(decimal), "1", "10000000",
            ParseLimitsInInvariantCulture = true,
            ErrorMessage = "قيمة القرض يجب أن تكون بين 1 و 10,000,000 دينار")]
        [LoanPrincipalPrecision]
        public decimal PrincipalAmount { get; set; }

        // القيمة الفعلية المعتمَدة تُعاد حسابها دومًا من الخادم (SEC-04) بدل الوثوق بهذا الحقل،
        // لكن الحد يبقى مفيدًا لأي استخدام مباشر آخر لهذا الـ DTO مستقبلاً.
        [Required]
        [Range(0, 100, ErrorMessage = "معدل الفائدة يجب أن يكون بين 0 و 100")]
        public decimal AnnualInterestRate { get; set; }

        [Required]
        [Range(1, 600, ErrorMessage = "مدة القرض يجب أن تكون بين شهر واحد و 600 شهر (50 سنة)")]
        public int DurationMonths { get; set; }

        [Required]
        [EnumDataType(typeof(PaymentFrequency))]
        public PaymentFrequency PaymentFrequency { get; set; }
        [Required]
        [EnumDataType(typeof(InterestType))]
        public InterestType InterestType { get; set; }
    }

    public class PaymentScheduleRowDto
    {
        public int PaymentNumber { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal PaymentAmount { get; set; }
        public decimal PrincipalPaid { get; set; }
        public decimal InterestPaid { get; set; }
        public decimal RemainingBalance { get; set; }
    }

    public class LoanCalculationResultDto
    {
        public decimal InstallmentAmount { get; set; }
        public decimal TotalInterest { get; set; }
        public decimal TotalRepayment { get; set; }
        public List<PaymentScheduleRowDto> Schedule { get; set; } = new List<PaymentScheduleRowDto>();
    }
}