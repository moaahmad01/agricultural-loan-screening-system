using System.ComponentModel.DataAnnotations;

namespace AgriculturalLoanSystem.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "National ID is required / الرقم الوطني مطلوب")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "National ID must be 10 digits / الرقم الوطني يجب أن يكون 10 خانات")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "National ID must contain digits only / الرقم الوطني يجب أن يتكون من أرقام فقط")]
        public string NationalId { get; set; } = string.Empty;
        [Required]
        public string FullNameAr { get; set; } = string.Empty;
        [Required(ErrorMessage = "Phone number is required / رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "Invalid phone number / رقم الهاتف غير صحيح")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required / كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Compare("Password", ErrorMessage = "Passwords do not match / كلمات المرور غير متطابقة")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
