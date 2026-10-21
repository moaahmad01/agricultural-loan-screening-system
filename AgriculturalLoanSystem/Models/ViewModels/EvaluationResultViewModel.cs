namespace AgriculturalLoanSystem.Models.ViewModels
{
    public class EvaluationResultViewModel
    {
        public int SessionId { get; set; }
        public int ClientId { get; set; }
        public AgriculturalLoanSystem.Data.Enums.Recommendation Recommendation { get; set; }

        public int YesCount { get; set; }

        public int TotalQuestions { get; set; }
        public decimal Score { get; set; }

        public int NoCount => TotalQuestions - YesCount;

        public string FormattedScore => Score.ToString("F2") + "%";
        public List<DocumentDto> AttachedDocuments { get; set; } = new List<DocumentDto>();

    }
    public class DocumentDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string SectionNameAr { get; set; } = string.Empty;
        public string SectionNameEn { get; set; } = string.Empty;

    }

}