using BankingApi.Application.Auth.Commands;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BankingApi.Api.Controllers;

/// <summary>
/// Handles user registration and authentication.
/// </summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMessageBus _bus;

    public AuthController(IMessageBus bus) => _bus = bus;

    /// <summary>
    /// Register a new customer account.
    /// </summary>
    /// <param name="request">Registration details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New user and account details.</returns>
    /// <response code="201">User and account created successfully.</response>
    /// <response code="422">Validation failed — field errors returned.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken ct)
    {
        var command = new RegisterUserCommand(
            FirstName: request.FirstName,
            MiddleName: request.MiddleName,
            LastName: request.LastName,
            Gender: request.Gender,
            Email: request.Email,
            Password: request.Password,
            BVN: request.BVN,
            Address: request.Address,
            State: request.State,
            Country: request.Country);

        var result = await _bus.InvokeAsync<RegisterUserResult>(command, ct);

        return CreatedAtAction(
            actionName: null,
            value: result);
    }

    /// <summary>
    /// Authenticate and obtain a JWT token.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JWT token and expiry.</returns>
    /// <response code="200">Login successful — token returned.</response>
    /// <response code="401">Invalid email or password.</response>
    /// <response code="422">Validation failed — field errors returned.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var command = new LoginCommand(
            Email: request.Email,
            Password: request.Password);

        var result = await _bus.InvokeAsync<LoginResult>(command, ct);

        return Ok(result);
    }
}

public record RegisterUserRequest(
    string FirstName,
    string? MiddleName,
    string LastName,
    string Gender,
    string Email,
    string Password,
    string BVN,
    string? Address,
    string? State,
    string Country);

public record LoginRequest(
    string Email,
    string Password);