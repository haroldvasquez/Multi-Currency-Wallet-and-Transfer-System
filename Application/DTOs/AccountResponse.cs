namespace Application.DTOs
{
    public class AccountResponse
    {
        public Guid AccountId { get; set; }
        public Guid CustomerId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal Balance { get; set; }
        public string AccountStatus { get; set; } = string.Empty;
    }
}
