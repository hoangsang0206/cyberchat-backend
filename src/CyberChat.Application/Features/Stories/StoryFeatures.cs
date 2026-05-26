using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using MediatR;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Application.Common.Models;
using CyberChat.Application.Features.Auth;
using CyberChat.Domain.Entities;
using CyberChat.Domain.Enums;

namespace CyberChat.Application.Features.Stories;

// DTOs
public record StoryDto(
    Guid Id,
    Guid UserId,
    UserDto User,
    string MediaUrl,
    StoryMediaType MediaType,
    string? Caption,
    string? BgColor,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    List<StoryViewDto> Views,
    List<StoryReactionDto> Reactions
);

public record StoryViewDto(
    Guid Id,
    Guid StoryId,
    Guid ViewerId,
    string ViewerName,
    DateTimeOffset ViewedAt
);

public record StoryReactionDto(
    Guid Id,
    Guid StoryId,
    Guid UserId,
    string Emoji,
    DateTimeOffset CreatedAt
);

// Create Story
public record CreateStoryCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    StoryMediaType MediaType,
    string? Caption,
    string? BgColor
) : IRequest<Result<StoryDto>>;

public class CreateStoryValidator : AbstractValidator<CreateStoryCommand>
{
    public CreateStoryValidator()
    {
        RuleFor(x => x.MediaType).IsInEnum();
        RuleFor(x => x.Caption).MaximumLength(500);
        RuleFor(x => x.BgColor).MaximumLength(7).Matches("^#([A-Fa-f0-9]{6})$").When(x => !string.IsNullOrEmpty(x.BgColor));
    }
}

public class CreateStoryHandler : IRequestHandler<CreateStoryCommand, Result<StoryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStorageService _storageService;
    private readonly ISignalRService _signalRService;
    private readonly IMapper _mapper;

    public CreateStoryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IStorageService storageService,
        ISignalRService signalRService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _signalRService = signalRService;
        _mapper = mapper;
    }

    public async Task<Result<StoryDto>> Handle(CreateStoryCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure<StoryDto>("Unauthorized.");

        var key = $"stories/{currentUserId}/{Guid.NewGuid()}_{request.FileName}";
        var mediaUrl = await _storageService.UploadFileAsync("cyberchat-stories", key, request.FileStream, request.ContentType, cancellationToken);

        var story = new Story
        {
            UserId = currentUserId.Value,
            MediaUrl = mediaUrl,
            MediaType = request.MediaType,
            Caption = request.Caption,
            BgColor = request.BgColor,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        await _unitOfWork.Stories.AddAsync(story, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Load user info for mapping
        var user = await _unitOfWork.Users.GetByIdAsync(currentUserId.Value, cancellationToken);
        story.User = user!;

        var dto = _mapper.Map<StoryDto>(story);

        // Realtime notify friends
        var friends = await _unitOfWork.Friends.GetFriendsAsync(currentUserId.Value, cancellationToken);
        var friendIds = friends.Select(f => f.Id);
        await _signalRService.SendToUsersAsync(friendIds, "OnStoryReceived", dto, cancellationToken);

        return dto;
    }
}

// Get Active Stories (Fetch stories for user and friends)
public record GetActiveStoriesQuery : IRequest<Result<List<StoryDto>>>;

public class GetActiveStoriesHandler : IRequestHandler<GetActiveStoriesQuery, Result<List<StoryDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetActiveStoriesHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<List<StoryDto>>> Handle(GetActiveStoriesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<List<StoryDto>>("Unauthorized.");

        var stories = await _unitOfWork.Stories.GetActiveStoriesAsync(userId.Value, cancellationToken);
        return _mapper.Map<List<StoryDto>>(stories);
    }
}

// View Story
public record ViewStoryCommand(Guid StoryId) : IRequest<Result>;

public class ViewStoryHandler : IRequestHandler<ViewStoryCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ViewStoryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(ViewStoryCommand request, CancellationToken cancellationToken)
    {
        var viewerId = _currentUserService.UserId;
        if (viewerId == null) return Result.Failure("Unauthorized.");

        var story = await _unitOfWork.Stories.GetByIdAsync(request.StoryId, cancellationToken);
        if (story == null || story.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Result.Failure("Active story not found.");
        }

        var existingView = await _unitOfWork.Stories.GetViewAsync(request.StoryId, viewerId.Value, cancellationToken);
        if (existingView == null)
        {
            var view = new StoryView
            {
                StoryId = request.StoryId,
                ViewerId = viewerId.Value
            };

            await _unitOfWork.Stories.AddViewAsync(view, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

// React to Story
public record ReactToStoryCommand(Guid StoryId, string Emoji) : IRequest<Result>;

public class ReactToStoryValidator : AbstractValidator<ReactToStoryCommand>
{
    public ReactToStoryValidator()
    {
        RuleFor(x => x.StoryId).NotEmpty();
        RuleFor(x => x.Emoji).NotEmpty().MaximumLength(10);
    }
}

public class ReactToStoryHandler : IRequestHandler<ReactToStoryCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;

    public ReactToStoryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
    }

    public async Task<Result> Handle(ReactToStoryCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var story = await _unitOfWork.Stories.GetByIdAsync(request.StoryId, cancellationToken);
        if (story == null || story.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Result.Failure("Active story not found.");
        }

        var existingReaction = await _unitOfWork.Stories.GetReactionAsync(request.StoryId, currentUserId.Value, cancellationToken);

        if (existingReaction != null)
        {
            _unitOfWork.Stories.RemoveReaction(existingReaction);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Notify
            await _signalRService.SendToUserAsync(story.UserId, "OnStoryReacted", new
            {
                StoryId = story.Id,
                UserId = currentUserId.Value,
                Action = "Removed"
            }, cancellationToken);
        }
        else
        {
            var reaction = new StoryReaction
            {
                StoryId = request.StoryId,
                UserId = currentUserId.Value,
                Emoji = request.Emoji
            };

            await _unitOfWork.Stories.AddReactionAsync(reaction, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Notify
            await _signalRService.SendToUserAsync(story.UserId, "OnStoryReacted", new
            {
                StoryId = story.Id,
                UserId = currentUserId.Value,
                Emoji = request.Emoji,
                Action = "Added"
            }, cancellationToken);
        }

        return Result.Success();
    }
}

// AutoMapper Profile
public class StoryMappingProfile : Profile
{
    public StoryMappingProfile()
    {
        CreateMap<Story, StoryDto>();
        CreateMap<StoryView, StoryViewDto>()
            .ForMember(dest => dest.ViewerName, opt => opt.MapFrom(src => src.Viewer.DisplayName));
        CreateMap<StoryReaction, StoryReactionDto>();
    }
}
