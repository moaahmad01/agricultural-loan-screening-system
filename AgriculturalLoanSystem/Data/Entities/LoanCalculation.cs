using AgriculturalLoanSystem.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriculturalLoanSystem.Data.Entities
{
    public class LoanCalculation
    {
        public int Id { get; set; }

        public int? SessionId { get; set; }
        public ScreeningSession? Session { get; set; }

        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;

        // (DB-01 — إصلاح): كان هذا الحقل نصًا حرًا (string) رغم وجود enum مخصَّص LoanCategory
        // غير مستخدم إطلاقًا في أي مكان، بينما القيم الفعلية المستخدمة ("SeasonalShort"/"MediumLong"
        // في بيانات البذر) لم تكن مطابقة أصلاً لأسماء الـ enum ("SeasonalShortTerm"/"MediumLongTerm").
        // الآن الحقل يستخدم الـ enum الحقيقي مباشرة، فلا يمكن إدخال قيمة غير صالحة إطلاقًا.
        [Required]
        public LoanCategory LoanCategory { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal PrincipalAmount { get; set; }
        public int DurationMonths { get; set; }
        public int NumberOfPayments { get; set; }

        public PaymentFrequency PaymentFrequency { get; set; } = PaymentFrequency.Monthly;

        [Column(TypeName = "decimal(5, 2)")] public decimal AnnualInterestRate { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal TotalInterest { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal InstallmentAmount { get; set; }

        public InterestType InterestType { get; set; } = InterestType.Simple;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public LoanApplicationStatus Status { get; set; } = LoanApplicationStatus.PendingApproval;
        public ICollection<PaymentSchedule> PaymentSchedules { get; set; } = new List<PaymentSchedule>();
    }

    public class PaymentSchedule
    {
        public int Id { get; set; }

        public int LoanCalculationId { get; set; }
        [ForeignKey("LoanCalculationId")] public LoanCalculation LoanCalculation { get; set; } = null!;

        public int PaymentNumber { get; set; }
        public DateTime? PaymentDate { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal PaymentAmount { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal PrincipalPaid { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal InterestPaid { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal RemainingBalance { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class InterestRate
    {
        public int Id { get; set; }

        // (DB-01): كانت هذه القيمة نصًا حرًا (string) تُطابَق يدويًا بمقارنة نصية مع الفئة المطلوبة
        // — الآن تستخدم نفس الـ enum المستخدم في LoanCalculation.LoanCategory لضمان التطابق دومًا.
        public LoanCategory LoanType { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal? MinLoanAmount { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal? MaxLoanAmount { get; set; }

        [Column(TypeName = "decimal(5, 2)")]
        public decimal AnnualRate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}