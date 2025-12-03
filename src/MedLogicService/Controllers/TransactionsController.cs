using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MedLogicService.DTOs;
using MedLogicService.Models;
using TenantDBRouter;
using TenantDBRouter.Services;

namespace MedLogicService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TenantDBRouter.TenantDBRouter _dbRouter;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        TenantDBRouter.TenantDBRouter dbRouter,
        ITenantContextAccessor tenantContext,
        ILogger<TransactionsController> logger)
    {
        _dbRouter = dbRouter ?? throw new ArgumentNullException(nameof(dbRouter));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions()
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("Request missing X-Tenant-Id header");
            return BadRequest(new { error = "Missing X-Tenant-Id header" });
        }

        _logger.LogInformation("Getting transactions for tenant {TenantId}", tenantId);

        try
        {
            var transactions = await _dbRouter.ExecuteQueryAsync(
                tenantId,
                async (connection) =>
                {
                    var results = new List<TransactionResponse>();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT Id, Amount, Description, CreatedAt FROM Transactions ORDER BY CreatedAt DESC";

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        results.Add(new TransactionResponse
                        {
                            Id = reader.GetInt64(0),
                            Amount = reader.GetDecimal(1),
                            Description = reader.GetString(2),
                            CreatedAt = reader.GetDateTime(3)
                        });
                    }

                    return results;
                }
            );

            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTransaction(long id)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("Request missing X-Tenant-Id header");
            return BadRequest(new { error = "Missing X-Tenant-Id header" });
        }

        _logger.LogInformation("Getting transaction {TransactionId} for tenant {TenantId}", id, tenantId);

        try
        {
            var transaction = await _dbRouter.ExecuteQueryAsync(
                tenantId,
                async (connection) =>
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT Id, Amount, Description, CreatedAt FROM Transactions WHERE Id = @Id";
                    command.Parameters.Add(new SqlParameter("@Id", id));

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        return new TransactionResponse
                        {
                            Id = reader.GetInt64(0),
                            Amount = reader.GetDecimal(1),
                            Description = reader.GetString(2),
                            CreatedAt = reader.GetDateTime(3)
                        };
                    }

                    return null;
                }
            );

            if (transaction == null)
            {
                return NotFound(new { error = $"Transaction {id} not found" });
            }

            return Ok(transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction {TransactionId} for tenant {TenantId}", id, tenantId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] TransactionRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("Request missing X-Tenant-Id header");
            return BadRequest(new { error = "Missing X-Tenant-Id header" });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Creating transaction for tenant {TenantId}: Amount={Amount}, Description={Description}", 
            tenantId, request.Amount, request.Description);

        try
        {
            var transaction = await _dbRouter.ExecuteQueryAsync(
                tenantId,
                async (connection) =>
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO Transactions (Amount, Description, CreatedAt) 
                        OUTPUT INSERTED.Id, INSERTED.Amount, INSERTED.Description, INSERTED.CreatedAt
                        VALUES (@Amount, @Description, @CreatedAt)";
                    
                    command.Parameters.Add(new SqlParameter("@Amount", request.Amount));
                    command.Parameters.Add(new SqlParameter("@Description", request.Description));
                    command.Parameters.Add(new SqlParameter("@CreatedAt", DateTime.UtcNow));

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        return new TransactionResponse
                        {
                            Id = reader.GetInt64(0),
                            Amount = reader.GetDecimal(1),
                            Description = reader.GetString(2),
                            CreatedAt = reader.GetDateTime(3)
                        };
                    }

                    throw new InvalidOperationException("Failed to create transaction");
                }
            );

            _logger.LogInformation("Created transaction {TransactionId} for tenant {TenantId}", transaction?.Id, tenantId);

            return CreatedAtAction(nameof(GetTransaction), new { id = transaction?.Id }, transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transaction for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(long id)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("Request missing X-Tenant-Id header");
            return BadRequest(new { error = "Missing X-Tenant-Id header" });
        }

        _logger.LogInformation("Deleting transaction {TransactionId} for tenant {TenantId}", id, tenantId);

        try
        {
            var deleted = await _dbRouter.ExecuteQueryAsync(
                tenantId,
                async (connection) =>
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM Transactions WHERE Id = @Id";
                    command.Parameters.Add(new SqlParameter("@Id", id));

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            );

            if (!deleted)
            {
                return NotFound(new { error = $"Transaction {id} not found" });
            }

            _logger.LogInformation("Deleted transaction {TransactionId} for tenant {TenantId}", id, tenantId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting transaction {TransactionId} for tenant {TenantId}", id, tenantId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
