namespace Application.DTOs;

public class AccountBalanceDetail
{
    public Guid    AccountId                 { get; set; }
    public string  AccountNumber             { get; set; } = null!;
    public string  AccountCurrency           { get; set; } = null!;
    public decimal CurrentBalance            { get; set; }
    public decimal CurrentBalanceConverted   { get; set; }
    public int     PeriodMovementsCount      { get; set; }
    public decimal PeriodTotalCredits        { get; set; }
    public decimal PeriodTotalDebits         { get; set; }
    public decimal PeriodNetChange           { get; set; }
}
