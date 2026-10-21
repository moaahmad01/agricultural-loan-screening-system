using System.ComponentModel.DataAnnotations;

namespace AgriculturalLoanSystem.Data.Entities
{
    public class Section
    {
        public int Id { get; set; }

        [Required, MaxLength(255)]
        public string SectionNameAr { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string SectionNameEn { get; set; } = string.Empty;
        [Required]
        [Range(0, 100)]
        public int Weight { get; set; } = 0; public int DisplayOrder { get; set; } = 0; public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}