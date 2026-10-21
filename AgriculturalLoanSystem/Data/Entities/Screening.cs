using AgriculturalLoanSystem.Data.Enums;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriculturalLoanSystem.Data.Entities
{
    public class ScreeningSession
    {
        public int Id { get; set; }

        public int ClientId { get; set; }
        public virtual Client Client { get; set; } = null!;
        public string? ApplicationUserId { get; set; }
        public virtual IdentityUser? ApplicationUser { get; set; }

        // (DB-05): تم حذف حقل UserId (int) الذي كان موجودًا هنا بلا أي استخدام فعلي في أي مكان
        // بالمشروع — القيمة الحقيقية لهوية المستخدم المسؤول عن الجلسة مخزّنة في ApplicationUserId
        // أعلاه فقط. وجود الحقلين معًا كان يُربك أي مطوّر يحاول معرفة أيهما مصدر الحقيقة.
        public SessionStatus SessionStatus { get; set; } = SessionStatus.InProgress;

        public int TotalYesAnswers { get; set; } = 0;
        public int TotalNoAnswers { get; set; } = 0;
        [Column(TypeName = "decimal(5, 2)")]
        public decimal Score { get; set; } = 0;

        public Recommendation? Recommendation { get; set; }
        public string? RecommendationReason { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<ScreeningDocument> Documents { get; set; } = new List<ScreeningDocument>();

        public ICollection<ScreeningAnswer> Answers { get; set; } = new List<ScreeningAnswer>();

        public ICollection<LoanCalculation> LoanCalculations { get; set; } = new List<LoanCalculation>();
    }
    public class ScreeningDocument
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty; public string FilePath { get; set; } = string.Empty; public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public int SessionId { get; set; }
        public virtual ScreeningSession? Session { get; set; }

        public int SectionId { get; set; }
        public virtual Section? Section { get; set; }
    }

    public class ScreeningAnswer
    {
        public int Id { get; set; }

        public int SessionId { get; set; }
        public ScreeningSession Session { get; set; } = null!;

        public int QuestionId { get; set; }
        public virtual Question Question { get; set; } = null!;

        public AnswerType Answer { get; set; }
        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}