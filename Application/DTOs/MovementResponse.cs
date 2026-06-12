namespace Application.DTOs
{
    public class MovementResponse
    {
        public Guid MovementId { get; set; }
        public Guid AccountId { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal NewBalance { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
