using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CyberChat.Application.Common.Models;
using CyberChat.Application.Features.Auth;
using CyberChat.Application.Features.Friends;

namespace CyberChat.API.Controllers;

[Authorize]
public class FriendsController : ApiControllerBase
{
    [HttpPost("request")]
    public async Task<ActionResult<Result<FriendRequestDto>>> SendRequest([FromBody] SendFriendRequestCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("request/{requestId:guid}/accept")]
    public async Task<ActionResult<Result>> AcceptRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AcceptFriendRequestCommand(requestId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("request/{requestId:guid}/reject")]
    public async Task<ActionResult<Result>> RejectRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RejectFriendRequestCommand(requestId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("request/{requestId:guid}/cancel")]
    public async Task<ActionResult<Result>> CancelRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CancelFriendRequestCommand(requestId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("{friendId:guid}")]
    public async Task<ActionResult<Result>> RemoveFriend(Guid friendId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RemoveFriendCommand(friendId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("block/{blockedId:guid}")]
    public async Task<ActionResult<Result>> BlockUser(Guid blockedId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new BlockUserCommand(blockedId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("block/{blockedId:guid}")]
    public async Task<ActionResult<Result>> UnblockUser(Guid blockedId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new UnblockUserCommand(blockedId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<Result<List<UserDto>>>> GetFriends(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetFriendsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("pending")]
    public async Task<ActionResult<Result<List<FriendRequestDto>>>> GetPendingRequests(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPendingRequestsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("blocked")]
    public async Task<ActionResult<Result<List<UserDto>>>> GetBlockedUsers(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetBlockedUsersQuery(), cancellationToken);
        return Ok(result);
    }
}
