using System.ComponentModel.DataAnnotations;

namespace MedDispenseSimulator.Models;

/// <summary>
/// 事务请求模型（与 MedLogicService 保持一致）
/// </summary>
public class TransactionRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; } = string.Empty;
}