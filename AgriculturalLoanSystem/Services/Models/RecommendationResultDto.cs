using AgriculturalLoanSystem.Data.Enums;
namespace AgriculturalLoanSystem.Services.Models
{

    public class RecommendationResultDto
    {
        public Recommendation Status { get; set; }
        public string ReasonAr { get; set; } = string.Empty;
        public string ReasonEn { get; set; } = string.Empty;
        public decimal FinalScore { get; set; }

    }
}