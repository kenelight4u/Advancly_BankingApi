using System.Security.Claims;
using BankingApi.Application.Transactions.Commands;
using BankingApi.Application.Transactions.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BankingApi.Api.Controllers;

/// <summary>
/// Handles fund transfers and transaction history for authenticated users.
/// </summary>
[ApiController]
[Route("api/transactions")]
[Authorize]
[Tags("Transactions")]
[Produces("application/json")]
public class TransactionController : ControllerBase
{
    private readonly IMessageBus _bus;

    public TransactionController(IMessageBus bus) => _bus = bus;

    /// <summary>
    /// Transfer funds to another customer account.
    /// The sender is always the authenticated user — SenderId is never
    /// accepted from the request body.
    /// Produces exactly three ledger legs sharing a Reference:
    /// CustomerTransfer → FeeCapture → NGLDebit.
    /// </summary>
    /// <param name="request">Transfer details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Transfer result with reference, amounts, and status.</returns>
    /// <response code="200">Transfer completed successfully.</response>
    /// <response code="400">Insufficient funds.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="404">Sender or recipient account not found.</response>
    /// <response code="422">Validation failed — field errors returned.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(TransferFundsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferFundsRequest request,
        CancellationToken ct)
    {
        var senderId = GetUserIdFromClaims();

        var command = new TransferFundsCommand(
            SenderId: senderId,
            DestAccountNumber: request.DestAccountNumber,
            Amount: request.Amount,
            Narration: request.Narration);

        var result = await _bus.InvokeAsync<TransferFundsResult>(command, ct);

        return Ok(result);
    }

    /// <summary>
    /// Retrieve the authenticated user's transaction history.
    /// Returns only transactions involving the user's account number.
    /// Legs are grouped by Reference — each transfer appears as one record
    /// with three legs (CustomerTransfer, FeeCapture, NGLDebit).
    /// Supports pagination via pageNumber and pageSize query parameters.
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1).</param>
    /// <param name="pageSize">Results per page, max 100 (default: 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated transaction groups.</returns>
    /// <response code="200">Transaction history returned.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="404">Account not found for the authenticated user.</response>
    /// <response code="422">Invalid pagination parameters.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(GetTransactionHistoryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserIdFromClaims();

        var query = new GetTransactionHistoryQuery(
            UserId: userId,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await _bus.InvokeAsync<GetTransactionHistoryResult>(query, ct);

        return Ok(result);
    }

    private Guid GetUserIdFromClaims()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");

        if (sub is null || !Guid.TryParse(sub, out var userId))
            throw new Application.Common.Exceptions.UnauthorizedException(
                "UserId claim is missing or malformed in the JWT token.");

        return userId;
    }
}

public record TransferFundsRequest(
    string DestAccountNumber,
    decimal Amount,
    string? Narration);