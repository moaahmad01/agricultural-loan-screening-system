using AgriculturalLoanSystem.Data.Enums;
using AgriculturalLoanSystem.Services.Models;
namespace AgriculturalLoanSystem.Models.ViewModels
{
    public class LoanCalculatorViewModel
    {
        public int ClientId { get; set; }
        public int? SessionId { get; set; }

        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
        public string ClientName { get; set; } = string.Empty;
        public bool HasActivePreviousLoan { get; set; }
        public string? PreviousLoanWarning { get; set; }
        public LoanInputModel Input { get; set; } = new LoanInputModel();

        // (DB-01): كان نصًا حرًا بقيمة افتراضية "SeasonalShort" لا تُطابق اسم أي enum فعلي.
        [System.ComponentModel.DataAnnotations.EnumDataType(typeof(LoanCategory))]
        public LoanCategory LoanCategory { get; set; } = LoanCategory.SeasonalShortTerm;
        public LoanCalculationResultDto? Result { get; set; }
    }
}