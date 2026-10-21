using System.ComponentModel.DataAnnotations;

namespace AgriculturalLoanSystem.Data.Entities
{
    public class Question
    {
        public int Id { get; set; }

        [Required]
        public int SectionId { get; set; }
        public virtual Section? Section { get; set; } = null!;

        [Required, MaxLength(1000)]
        public string QuestionTextAr { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string QuestionTextEn { get; set; } = string.Empty;

        [Range(1, 5, ErrorMessage = "Between 1 and 5")]
        public int PointValue { get; set; } = 1;

        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}