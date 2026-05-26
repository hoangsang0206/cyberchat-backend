using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using MediatR;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Application.Common.Models;
using CyberChat.Domain.Entities;
using CyberChat.Domain.Enums;

namespace CyberChat.Application.Features.Todos;

// DTOs
public record TodoListDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Color,
    string? Icon,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record TodoDto(
    Guid Id,
    Guid ListId,
    Guid UserId,
    string Title,
    string? Description,
    DateTimeOffset? DueDate,
    bool IsAllDay,
    TodoPriority Priority,
    TodoStatus Status,
    int? ReminderMinutes,
    string? RecurrenceRule,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<string> Tags,
    List<TodoAttachmentDto> Attachments
);

public record TodoAttachmentDto(
    Guid Id,
    Guid TodoId,
    string Url,
    string? FileName,
    long? FileSize,
    DateTimeOffset CreatedAt
);

public record TodoAttachmentInput(
    string Url,
    string? FileName,
    long? FileSize
);

// Create Todo List
public record CreateTodoListCommand(
    string Title,
    string Color,
    string? Icon,
    bool IsDefault
) : IRequest<Result<TodoListDto>>;

public class CreateTodoListValidator : AbstractValidator<CreateTodoListCommand>
{
    public CreateTodoListValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(7).Matches("^#([A-Fa-f0-9]{6})$");
    }
}

public class CreateTodoListHandler : IRequestHandler<CreateTodoListCommand, Result<TodoListDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public CreateTodoListHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<TodoListDto>> Handle(CreateTodoListCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<TodoListDto>("Unauthorized.");

        var list = new TodoList
        {
            UserId = userId.Value,
            Title = request.Title,
            Color = request.Color,
            Icon = request.Icon,
            IsDefault = request.IsDefault
        };

        if (list.IsDefault)
        {
            // Reset other default lists
            var currentLists = await _unitOfWork.Todos.GetListsForUserAsync(userId.Value, cancellationToken);
            foreach (var existing in currentLists.Where(l => l.IsDefault))
            {
                existing.IsDefault = false;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Todos.UpdateList(existing);
            }
        }

        await _unitOfWork.Todos.AddListAsync(list, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TodoListDto>(list);
    }
}

// Update Todo List
public record UpdateTodoListCommand(
    Guid ListId,
    string Title,
    string Color,
    string? Icon,
    bool IsDefault
) : IRequest<Result<TodoListDto>>;

public class UpdateTodoListValidator : AbstractValidator<UpdateTodoListCommand>
{
    public UpdateTodoListValidator()
    {
        RuleFor(x => x.ListId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(7).Matches("^#([A-Fa-f0-9]{6})$");
    }
}

public class UpdateTodoListHandler : IRequestHandler<UpdateTodoListCommand, Result<TodoListDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public UpdateTodoListHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<TodoListDto>> Handle(UpdateTodoListCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<TodoListDto>("Unauthorized.");

        var list = await _unitOfWork.Todos.GetListByIdAsync(request.ListId, cancellationToken);
        if (list == null || list.UserId != userId.Value) return Result.Failure<TodoListDto>("Todo list not found.");

        list.Title = request.Title;
        list.Color = request.Color;
        list.Icon = request.Icon;
        
        if (request.IsDefault && !list.IsDefault)
        {
            list.IsDefault = true;
            // Reset other default lists
            var currentLists = await _unitOfWork.Todos.GetListsForUserAsync(userId.Value, cancellationToken);
            foreach (var existing in currentLists.Where(l => l.IsDefault && l.Id != list.Id))
            {
                existing.IsDefault = false;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Todos.UpdateList(existing);
            }
        }
        else
        {
            list.IsDefault = request.IsDefault;
        }

        list.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Todos.UpdateList(list);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TodoListDto>(list);
    }
}

// Delete Todo List
public record DeleteTodoListCommand(Guid ListId) : IRequest<Result>;

public class DeleteTodoListHandler : IRequestHandler<DeleteTodoListCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeleteTodoListHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(DeleteTodoListCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure("Unauthorized.");

        var list = await _unitOfWork.Todos.GetListByIdAsync(request.ListId, cancellationToken);
        if (list == null || list.UserId != userId.Value) return Result.Failure("Todo list not found.");

        _unitOfWork.Todos.DeleteList(list);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// Get Todo Lists
public record GetTodoListsQuery : IRequest<Result<List<TodoListDto>>>;

public class GetTodoListsHandler : IRequestHandler<GetTodoListsQuery, Result<List<TodoListDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetTodoListsHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<List<TodoListDto>>> Handle(GetTodoListsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<List<TodoListDto>>("Unauthorized.");

        var lists = await _unitOfWork.Todos.GetListsForUserAsync(userId.Value, cancellationToken);
        return _mapper.Map<List<TodoListDto>>(lists);
    }
}

// Create Todo
public record CreateTodoCommand(
    Guid ListId,
    string Title,
    string? Description,
    DateTimeOffset? DueDate,
    bool IsAllDay,
    TodoPriority Priority,
    int? ReminderMinutes,
    string? RecurrenceRule,
    List<string>? Tags,
    List<TodoAttachmentInput>? Attachments
) : IRequest<Result<TodoDto>>;

public class CreateTodoValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoValidator()
    {
        RuleFor(x => x.ListId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class CreateTodoHandler : IRequestHandler<CreateTodoCommand, Result<TodoDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public CreateTodoHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<TodoDto>> Handle(CreateTodoCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<TodoDto>("Unauthorized.");

        var list = await _unitOfWork.Todos.GetListByIdAsync(request.ListId, cancellationToken);
        if (list == null || list.UserId != userId.Value) return Result.Failure<TodoDto>("Todo list not found.");

        var todo = new Todo
        {
            ListId = request.ListId,
            UserId = userId.Value,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            IsAllDay = request.IsAllDay,
            Priority = request.Priority,
            Status = TodoStatus.Todo,
            ReminderMinutes = request.ReminderMinutes,
            RecurrenceRule = request.RecurrenceRule
        };

        if (request.Tags != null)
        {
            foreach (var t in request.Tags.Distinct())
            {
                todo.Tags.Add(new TodoTag { Tag = t });
            }
        }

        if (request.Attachments != null)
        {
            foreach (var att in request.Attachments)
            {
                todo.Attachments.Add(new TodoAttachment
                {
                    Url = att.Url,
                    FileName = att.FileName,
                    FileSize = att.FileSize
                });
            }
        }

        await _unitOfWork.Todos.AddTodoAsync(todo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TodoDto>(todo);
    }
}

// Update Todo
public record UpdateTodoCommand(
    Guid TodoId,
    Guid ListId,
    string Title,
    string? Description,
    DateTimeOffset? DueDate,
    bool IsAllDay,
    TodoPriority Priority,
    TodoStatus Status,
    int? ReminderMinutes,
    string? RecurrenceRule,
    List<string>? Tags,
    List<TodoAttachmentInput>? Attachments
) : IRequest<Result<TodoDto>>;

public class UpdateTodoValidator : AbstractValidator<UpdateTodoCommand>
{
    public UpdateTodoValidator()
    {
        RuleFor(x => x.TodoId).NotEmpty();
        RuleFor(x => x.ListId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateTodoHandler : IRequestHandler<UpdateTodoCommand, Result<TodoDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public UpdateTodoHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<TodoDto>> Handle(UpdateTodoCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<TodoDto>("Unauthorized.");

        var todo = await _unitOfWork.Todos.GetTodoByIdAsync(request.TodoId, cancellationToken);
        if (todo == null || todo.UserId != userId.Value) return Result.Failure<TodoDto>("Todo item not found.");

        // Check if moving to a different list
        if (todo.ListId != request.ListId)
        {
            var newList = await _unitOfWork.Todos.GetListByIdAsync(request.ListId, cancellationToken);
            if (newList == null || newList.UserId != userId.Value)
            {
                return Result.Failure<TodoDto>("Target todo list not found.");
            }
            todo.ListId = request.ListId;
        }

        todo.Title = request.Title;
        todo.Description = request.Description;
        todo.DueDate = request.DueDate;
        todo.IsAllDay = request.IsAllDay;
        todo.Priority = request.Priority;
        todo.ReminderMinutes = request.ReminderMinutes;
        todo.RecurrenceRule = request.RecurrenceRule;

        if (request.Status != todo.Status)
        {
            todo.Status = request.Status;
            todo.CompletedAt = request.Status == TodoStatus.Done ? DateTimeOffset.UtcNow : null;
        }

        todo.UpdatedAt = DateTimeOffset.UtcNow;

        // Tags Update logic
        var currentTags = todo.Tags.Select(t => t.Tag).ToList();
        var incomingTags = request.Tags ?? new List<string>();

        // Remove removed tags
        var toRemove = todo.Tags.Where(t => !incomingTags.Contains(t.Tag)).ToList();
        foreach (var tag in toRemove) todo.Tags.Remove(tag);

        // Add new tags
        var toAdd = incomingTags.Except(currentTags);
        foreach (var tagStr in toAdd)
        {
            todo.Tags.Add(new TodoTag { Tag = tagStr });
        }

        // Attachments Update logic
        todo.Attachments.Clear();
        if (request.Attachments != null)
        {
            foreach (var att in request.Attachments)
            {
                todo.Attachments.Add(new TodoAttachment
                {
                    Url = att.Url,
                    FileName = att.FileName,
                    FileSize = att.FileSize
                });
            }
        }

        _unitOfWork.Todos.UpdateTodo(todo);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map and return updated object
        return _mapper.Map<TodoDto>(todo);
    }
}

// Delete Todo
public record DeleteTodoCommand(Guid TodoId) : IRequest<Result>;

public class DeleteTodoHandler : IRequestHandler<DeleteTodoCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeleteTodoHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(DeleteTodoCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure("Unauthorized.");

        var todo = await _unitOfWork.Todos.GetTodoByIdAsync(request.TodoId, cancellationToken);
        if (todo == null || todo.UserId != userId.Value) return Result.Failure("Todo item not found.");

        _unitOfWork.Todos.DeleteTodo(todo);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// Get Todos paginated
public record GetTodosQuery(
    Guid? ListId,
    string? Status,
    string? Priority,
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedList<TodoDto>>>;

public class GetTodosHandler : IRequestHandler<GetTodosQuery, Result<PagedList<TodoDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetTodosHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<PagedList<TodoDto>>> Handle(GetTodosQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<PagedList<TodoDto>>("Unauthorized.");

        var pagedTodos = await _unitOfWork.Todos.GetTodosAsync(
            userId.Value,
            request.ListId,
            request.Status,
            request.Priority,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtoList = _mapper.Map<List<TodoDto>>(pagedTodos.Items);
        var pagedResult = new PagedList<TodoDto>(dtoList, pagedTodos.TotalCount, pagedTodos.PageNumber, pagedTodos.PageSize);

        return pagedResult;
    }
}

// AutoMapper Profile
public class TodoMappingProfile : Profile
{
    public TodoMappingProfile()
    {
        CreateMap<TodoList, TodoListDto>();
        CreateMap<Todo, TodoDto>()
            .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.Select(t => t.Tag).ToList()));
        CreateMap<TodoAttachment, TodoAttachmentDto>();
    }
}
