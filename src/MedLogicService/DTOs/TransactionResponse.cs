namespace MedLogicService.DTOs;

public class TransactionResponse
{
    public long Id { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
