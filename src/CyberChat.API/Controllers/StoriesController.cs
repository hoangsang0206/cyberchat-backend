using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CyberChat.Application.Common.Models;
using CyberChat.Application.Features.Stories;
using CyberChat.Domain.Enums;

namespace CyberChat.API.Controllers;

[Authorize]
public class StoriesController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Result<StoryDto>>> CreateStory(
        IFormFile file, 
        [FromQuery] StoryMediaType mediaType, 
        [FromQuery] string? caption, 
        [FromQuery] string? bgColor, 
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(Result.Failure<StoryDto>("No media file was uploaded."));
        }

        using var stream = file.OpenReadStream();
        var command = new CreateStoryCommand(stream, file.FileName, file.ContentType, mediaType, caption, bgColor);
        var result = await Mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("feed")]
    public async Task<ActionResult<Result<List<StoryDto>>>> GetFeed(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetActiveStoriesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{storyId:guid}/view")]
    public async Task<ActionResult<Result>> ViewStory(Guid storyId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ViewStoryCommand(storyId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("{storyId:guid}/react")]
    public async Task<ActionResult<Result>> ReactToStory(Guid storyId, [FromBody] string emoji, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ReactToStoryCommand(storyId, emoji), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
