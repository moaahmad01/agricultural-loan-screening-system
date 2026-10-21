using AgriculturalLoanSystem.Data.Entities;

namespace AgriculturalLoanSystem.Models.ViewModels
{
    public class AuditLogsViewModel
    {
        public List<AuditLog> Items { get; set; } = new();

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        // قيم الفلاتر المُستخدَمة حاليًا (تُعاد للعرض في نموذج الفلترة نفسه)
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? UserId { get; set; }
        public string? EntityName { get; set; }

        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}
