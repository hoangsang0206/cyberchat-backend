using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CyberChat.Application.Common.Models;
using CyberChat.Application.Features.Conversations;

namespace CyberChat.API.Controllers;

[Authorize]
public class ConversationsController : ApiControllerBase
{
    [HttpPost("direct")]
    public async Task<ActionResult<Result<ConversationDto>>> CreateDirect([FromBody] CreateDirectConversationCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("group")]
    public async Task<ActionResult<Result<ConversationDto>>> CreateGroup([FromBody] CreateGroupConversationCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("group/{conversationId:guid}/rename")]
    public async Task<ActionResult<Result>> RenameGroup(Guid conversationId, [FromBody] string newName, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RenameGroupCommand(conversationId, newName), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("group/{conversationId:guid}/members")]
    public async Task<ActionResult<Result>> AddMember(Guid conversationId, [FromBody] Guid memberId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AddMemberCommand(conversationId, memberId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("group/{conversationId:guid}/members/{memberId:guid}")]
    public async Task<ActionResult<Result>> RemoveMember(Guid conversationId, Guid memberId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RemoveMemberCommand(conversationId, memberId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("group/{conversationId:guid}/leave")]
    public async Task<ActionResult<Result>> LeaveGroup(Guid conversationId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new LeaveGroupCommand(conversationId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<Result<PagedList<ConversationDto>>>> GetConversations(
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 20, 
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new GetConversationsQuery(pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }
}
