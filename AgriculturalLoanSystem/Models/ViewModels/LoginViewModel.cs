using System.ComponentModel.DataAnnotations;

namespace AgriculturalLoanSystem.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "National ID is required / الرقم الوطني مطلوب")]
        public string NationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required / كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
