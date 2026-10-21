using AgriculturalLoanSystem.Data.Enums;
using System.ComponentModel.DataAnnotations.Schema;
namespace AgriculturalLoanSystem.Data.Entities
{
    public class PreviousLoan
    {
        public int Id { get; set; }

        public int ClientId { get; set; }

        public virtual Client Client { get; set; } = null!;

        public DateTime? LoanDate { get; set; }

        // (DB-01): نفس إصلاح LoanCalculation.LoanCategory أعلاه، تُركت Nullable لأن السجلات
        // التاريخية للقروض السابقة قد لا تحمل فئة موثَّقة دومًا.
        public LoanCategory? LoanCategory { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal? LoanAmount { get; set; }

        public LoanStatus Status { get; set; } = LoanStatus.Ongoing;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}