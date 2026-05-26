using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CyberChat.Application.Common.Models;
using CyberChat.Application.Features.Auth;
using CyberChat.Application.Features.Users;

namespace CyberChat.API.Controllers;

[Authorize]
public class UsersController : ApiControllerBase
{
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<Result<UserDto>>> GetProfile(Guid userId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetProfileQuery(userId), cancellationToken);
        if (result.IsFailure)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<Result<UserDto>>> UpdateProfile([FromBody] UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("avatar")]
    public async Task<ActionResult<Result<string>>> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(Result.Failure<string>("No file was uploaded."));
        }

        using var stream = file.OpenReadStream();
        var command = new UploadAvatarCommand(stream, file.FileName, file.ContentType);
        var result = await Mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<Result<PagedList<UserDto>>>> SearchUsers(
        [FromQuery] string? query, 
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 20, 
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new SearchUsersQuery(query, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }
}
