using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Application.Common.Models;
using CyberChat.Application.Features.Messages;
using CyberChat.Domain.Enums;

namespace CyberChat.API.Controllers;

[Authorize]
public class MessagesController : ApiControllerBase
{
    private readonly IStorageService _storageService;

    public MessagesController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    [HttpPost]
    public async Task<ActionResult<Result<MessageDto>>> SendMessage([FromBody] SendMessageCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("{messageId:guid}")]
    public async Task<ActionResult<Result<MessageDto>>> EditMessage(Guid messageId, [FromBody] string newContent, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new EditMessageCommand(messageId, newContent), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("{messageId:guid}")]
    public async Task<ActionResult<Result>> DeleteMessage(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteMessageCommand(messageId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("{messageId:guid}/react")]
    public async Task<ActionResult<Result>> ReactToMessage(Guid messageId, [FromBody] string emoji, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ReactToMessageCommand(messageId, emoji), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<ActionResult<Result<PagedList<MessageDto>>>> GetMessages(
        Guid conversationId, 
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 20, 
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new GetMessagesQuery(conversationId, pageNumber, pageSize), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("upload")]
    public async Task<ActionResult<Result<AttachmentInput>>> UploadAttachment(IFormFile file, [FromQuery] AttachmentType type, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(Result.Failure<AttachmentInput>("No file was uploaded."));
        }

        using var stream = file.OpenReadStream();
        var key = $"attachments/{Guid.NewGuid()}_{file.FileName}";
        var url = await _storageService.UploadFileAsync("cyberchat-attachments", key, stream, file.ContentType, cancellationToken);

        var attachment = new AttachmentInput(
            type,
            url,
            file.FileName,
            file.Length,
            file.ContentType,
            null, // width (can be set by client if they parse image details)
            null, // height
            null, // duration
            null  // sticker pack id
        );

        return Ok(Result.Success(attachment));
    }
}
