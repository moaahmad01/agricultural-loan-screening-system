namespace AgriculturalLoanSystem.Data.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string Action { get; set; } = string.Empty; public string EntityName { get; set; } = string.Empty; public string? EntityId { get; set; }
        public string? Changes { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}