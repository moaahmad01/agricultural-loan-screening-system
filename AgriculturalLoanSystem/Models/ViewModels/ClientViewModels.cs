using AgriculturalLoanSystem.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace AgriculturalLoanSystem.Models.ViewModels
{
    public class ClientDetailsViewModel
    {
        public int Id { get; set; }
        public string NationalId { get; set; } = string.Empty;
        public string? IdentityDocumentPath { get; set; }
        public string FullNameAr { get; set; } = string.Empty;
        public string FullNameEn { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public DateOnly? DateOfBirth { get; set; }
        public int Age
        {
            get
            {
                if (!DateOfBirth.HasValue || DateOfBirth.Value.Year <= 1) return 0;

                var today = DateOnly.FromDateTime(DateTime.Now);
                var age = today.Year - DateOfBirth.Value.Year;

                if (DateOfBirth.Value > today.AddYears(-age)) age--;

                return age;
            }
        }
        public string Region { get; set; } = string.Empty;
        public string DetailedArea { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public List<PreviousLoanDto> PreviousLoans { get; set; } = new List<PreviousLoanDto>();

        public List<LoanCalculation> LoanCalculations { get; set; } = new List<LoanCalculation>();

        public List<ScreeningSession> ScreeningSessions { get; set; } = new List<ScreeningSession>();

        public ScreeningSession? LastScreeningSession { get; set; }
    }

    public class ClientSearchViewModel
    {
        public string SearchQuery { get; set; } = string.Empty;
        public List<ClientSearchResult> Clients { get; set; } = new List<ClientSearchResult>();

        // (DB-07 — موديل 7): إضافة Pagination حقيقية كانت مفقودة — Search كانت تُعيد كل
        // النتائج المطابقة دفعة واحدة بلا أي حد، ما يصبح عنق زجاجة واضح مع نمو قاعدة العملاء.
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    public class ClientSearchResult
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;

        public string FullNameEn { get; set; } = string.Empty;

        public string NationalId { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Region { get; set; }
    }

    public class PreviousLoanDto
    {
        public string? LoanType { get; set; }
        public decimal? LoanAmount { get; set; }
        public string? Status { get; set; }
        public DateTime? LoanDate { get; set; }
    }

    // جديد (CQ-03 / موديل 2): كانت ميزة "إضافة عميل جديد" غير مكتملة أصلاً — Action بلا View وبلا
    // معالج POST. هذا الـ ViewModel مخصَّص لنموذج الإنشاء تحديدًا (بخلاف تعديل الكيان Client مباشرة).
    public class ClientCreateViewModel
    {
        [Required(ErrorMessage = "الرقم الوطني مطلوب / National ID is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "الرقم الوطني يجب أن يتكون من 10 أرقام / National ID must be 10 digits")]
        public string NationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالعربية مطلوب / Arabic name is required")]
        [MaxLength(255)]
        public string FullNameAr { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? FullNameEn { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        public string? Region { get; set; }
        public string? DetailedArea { get; set; }
        public string? Address { get; set; }
    }
}