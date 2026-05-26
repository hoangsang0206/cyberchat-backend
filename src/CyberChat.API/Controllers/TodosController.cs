using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CyberChat.Application.Common.Models;
using CyberChat.Application.Features.Todos;

namespace CyberChat.API.Controllers;

[Authorize]
public class TodosController : ApiControllerBase
{
    // --- LISTS CRUD ---

    [HttpPost("lists")]
    public async Task<ActionResult<Result<TodoListDto>>> CreateList([FromBody] CreateTodoListCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("lists/{listId:guid}")]
    public async Task<ActionResult<Result<TodoListDto>>> UpdateList(Guid listId, [FromBody] CreateTodoListCommand request, CancellationToken cancellationToken)
    {
        // Convert request into Update command
        var command = new UpdateTodoListCommand(listId, request.Title, request.Color, request.Icon, request.IsDefault);
        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("lists/{listId:guid}")]
    public async Task<ActionResult<Result>> DeleteList(Guid listId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteTodoListCommand(listId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("lists")]
    public async Task<ActionResult<Result<List<TodoListDto>>>> GetLists(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetTodoListsQuery(), cancellationToken);
        return Ok(result);
    }

    // --- TODOS CRUD ---

    [HttpPost]
    public async Task<ActionResult<Result<TodoDto>>> CreateTodo([FromBody] CreateTodoCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("{todoId:guid}")]
    public async Task<ActionResult<Result<TodoDto>>> UpdateTodo(Guid todoId, [FromBody] UpdateTodoCommand command, CancellationToken cancellationToken)
    {
        if (todoId != command.TodoId)
        {
            return BadRequest(Result.Failure<TodoDto>("Route ID does not match body ID."));
        }

        var result = await Mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("{todoId:guid}")]
    public async Task<ActionResult<Result>> DeleteTodo(Guid todoId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteTodoCommand(todoId), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<Result<PagedList<TodoDto>>>> GetTodos(
        [FromQuery] Guid? listId,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new GetTodosQuery(listId, status, priority, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }
}
