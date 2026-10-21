using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
namespace AgriculturalLoanSystem.Data.Entities
{
    public class Client
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string NationalId { get; set; } = string.Empty; public string? IdentityDocumentPath { get; set; }
        [Required]
        [MaxLength(255)]
        public string FullNameAr { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string FullNameEn { get; set; } = string.Empty;

        public DateOnly? DateOfBirth { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }


        [MaxLength(255)]
        public string? Region { get; set; }
        [MaxLength(500)]
        public string? DetailedArea { get; set; }

        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? ApplicationUserId { get; set; }

        public virtual IdentityUser? ApplicationUser { get; set; }

        public ICollection<PreviousLoan> PreviousLoans { get; set; } = new List<PreviousLoan>();

        public ICollection<ScreeningSession> ScreeningSessions { get; set; } = new List<ScreeningSession>();

        public ICollection<LoanCalculation> LoanCalculations { get; set; } = new List<LoanCalculation>();

    }
}