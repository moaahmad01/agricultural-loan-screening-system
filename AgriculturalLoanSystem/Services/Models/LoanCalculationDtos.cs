using AgriculturalLoanSystem.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace AgriculturalLoanSystem.Services.Models
{
    public class LoanInputModel
    {
        // (CQ-04 — إصلاح): [Required] على حقول قيمية (decimal/int) لا تأثير فعلي له (القيمة 0
        // تُعتبر "موجودة" دومًا)، فلم يكن هناك أي حد فعلي يمنع مدة صفر أو مبلغًا سالبًا/ضخمًا بلا حدود.
        [Required]
        [Range(1, 10_000_000, ErrorMessage = "قيمة القرض يجب أن تكون بين 1 و 10,000,000 دينار")]
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