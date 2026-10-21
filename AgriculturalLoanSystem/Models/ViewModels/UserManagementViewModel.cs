namespace AgriculturalLoanSystem.Models.ViewModels
{
    public class UserManagementViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty; public string FullNameAr { get; set; } = string.Empty;
        public string FullNameEn { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }
}