using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CyberChat.Application.Common.Models;
using CyberChat.Application.Features.Auth;

namespace CyberChat.API.Controllers;

public class AuthController : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<Result<AuthResponseDto>>> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<Result<AuthResponseDto>>> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        // IP Address extraction for session logs
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var enrichedCommand = command with { IpAddress = ipAddress };

        var result = await Mediator.Send(enrichedCommand, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<Result<AuthResponseDto>>> Refresh([FromBody] RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var enrichedCommand = command with { IpAddress = ipAddress };

        var result = await Mediator.Send(enrichedCommand, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<ActionResult<Result>> Logout([FromBody] LogoutCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<Result<UserDto>>> GetMe(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
