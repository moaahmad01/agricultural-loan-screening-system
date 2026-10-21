using System.ComponentModel.DataAnnotations;

namespace AgriculturalLoanSystem.Models.ViewModels
{
    public class ClientEditViewModel
    {
        public int Id { get; set; }

        [Display(Name = "الاسم الكامل")]
        public string FullNameAr { get; set; } = string.Empty;

        public IFormFile? IdentityDocumentFile { get; set; }

        public string? ExistingFilePath { get; set; }
    }
}