using System.Security.Claims;
using BankingApi.Application.Accounts.Commands;
using BankingApi.Application.Accounts.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BankingApi.Api.Controllers;

/// <summary>
/// Manages the authenticated user's bank account profile.
/// </summary>
[Authorize]
[ApiController]
[Route("api/accounts")]
[Tags("Accounts")]
[Produces("application/json")]
public class AccountController : ControllerBase
{
    private readonly IMessageBus _bus;

    public AccountController(IMessageBus bus) => _bus = bus;

    /// <summary>
    /// Retrieve the authenticated user's account and profile details.
    /// </summary>
    /// <returns>Full account profile.</returns>
    /// <response code="200">Account details returned.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="404">Account not found for the authenticated user.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(GetAccountResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyAccount(CancellationToken ct)
    {
        var userId = GetUserIdFromClaims(HttpContext);
        
        var query = new GetAccountQuery(UserId: userId);
        var result = await _bus.InvokeAsync<GetAccountResult>(query, ct);

        return Ok(result);
    }

    /// <summary>
    /// Update mutable profile fields for the authenticated user.
    /// Applies PATCH semantics — only provided (non-null) fields are updated.
    /// Immutable fields (Email, BVN, AccountNumber, Balance) cannot be changed here.
    /// </summary>
    /// <param name="request">Fields to update — all optional.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated account profile.</returns>
    /// <response code="200">Profile updated successfully.</response>
    /// <response code="401">Missing or invalid JWT token.</response>
    /// <response code="404">Account not found for the authenticated user.</response>
    /// <response code="422">Validation failed — field errors returned.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UpdateAccountResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateMyAccount(
        [FromBody] UpdateAccountRequest request,
        CancellationToken ct)
    {
        var userId = GetUserIdFromClaims(HttpContext);
        
        var command = new UpdateAccountCommand(
            UserId: userId,
            FirstName: request.FirstName,
            MiddleName: request.MiddleName,
            LastName: request.LastName,
            Gender: request.Gender,
            Address: request.Address,
            State: request.State,
            Country: request.Country);

        var result = await _bus.InvokeAsync<UpdateAccountResult>(command, ct);

        return Ok(result);
    }

    /// <summary>
    /// Extracts and parses the UserId from the JWT "sub" claim.
    /// Throws 401 if the claim is missing or malformed — this should
    /// never happen on a properly [Authorize]-protected endpoint.
    /// </summary>
    private Guid GetUserIdFromClaims(HttpContext context)
    {
        var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? context.User.FindFirstValue("sub");

        if (sub is null || !Guid.TryParse(sub, out var userId))
            throw new Application.Common.Exceptions.UnauthorizedException(
                "UserId claim is missing or malformed in the JWT token.");

        return userId;
    }
}

public record UpdateAccountRequest(
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Gender,
    string? Address,
    string? State,
    string? Country);