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
using CyberChat.Application.Features.Auth;
using CyberChat.Domain.Entities;
using CyberChat.Domain.Enums;

namespace CyberChat.Application.Features.Conversations;

// DTOs
public record ConversationDto(
    Guid Id,
    ConversationType Type,
    string? Name,
    string? AvatarUrl,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<ConversationMemberDto> Members,
    bool IsMuted,
    int UnreadCount,
    string? LastMessageText,
    DateTimeOffset? LastMessageTime
);

public record ConversationMemberDto(
    Guid Id,
    Guid UserId,
    MemberRole Role,
    DateTimeOffset JoinedAt,
    UserDto User
);

// Create Direct Conversation
public record CreateDirectConversationCommand(Guid OtherUserId) : IRequest<Result<ConversationDto>>;

public class CreateDirectConversationHandler : IRequestHandler<CreateDirectConversationCommand, Result<ConversationDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;
    private readonly IMapper _mapper;

    public CreateDirectConversationHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
        _mapper = mapper;
    }

    public async Task<Result<ConversationDto>> Handle(CreateDirectConversationCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure<ConversationDto>("Unauthorized.");
        if (currentUserId.Value == request.OtherUserId) return Result.Failure<ConversationDto>("Cannot chat with yourself.");

        // Check if direct conversation already exists
        var existing = await _unitOfWork.Conversations.GetDirectConversationAsync(currentUserId.Value, request.OtherUserId, cancellationToken);
        if (existing != null)
        {
            return _mapper.Map<ConversationDto>(existing);
        }

        // Check block relationship
        if (await _unitOfWork.Friends.IsBlockedAsync(currentUserId.Value, request.OtherUserId, cancellationToken) ||
            await _unitOfWork.Friends.IsBlockedAsync(request.OtherUserId, currentUserId.Value, cancellationToken))
        {
            return Result.Failure<ConversationDto>("Cannot start conversation. Blocking relation exists.");
        }

        var conversation = new Conversation
        {
            Type = ConversationType.Direct,
            CreatedBy = currentUserId.Value
        };

        var memberA = new ConversationMember
        {
            Conversation = conversation,
            UserId = currentUserId.Value,
            Role = MemberRole.Owner
        };

        var memberB = new ConversationMember
        {
            Conversation = conversation,
            UserId = request.OtherUserId,
            Role = MemberRole.Member
        };

        conversation.Members.Add(memberA);
        conversation.Members.Add(memberB);

        await _unitOfWork.Conversations.AddAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map and reload to ensure Navigations are loaded
        var reloaded = await _unitOfWork.Conversations.GetByIdAsync(conversation.Id, cancellationToken);
        var dto = _mapper.Map<ConversationDto>(reloaded!);

        // Notify other user
        await _signalRService.SendToUserAsync(request.OtherUserId, "OnConversationCreated", dto, cancellationToken);

        return dto;
    }
}

// Create Group Conversation
public record CreateGroupConversationCommand(
    string Name,
    List<Guid> MemberIds
) : IRequest<Result<ConversationDto>>;

public class CreateGroupConversationValidator : AbstractValidator<CreateGroupConversationCommand>
{
    public CreateGroupConversationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MemberIds).NotEmpty().WithMessage("Group must have at least one additional member.");
    }
}

public class CreateGroupConversationHandler : IRequestHandler<CreateGroupConversationCommand, Result<ConversationDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;
    private readonly IMapper _mapper;

    public CreateGroupConversationHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
        _mapper = mapper;
    }

    public async Task<Result<ConversationDto>> Handle(CreateGroupConversationCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure<ConversationDto>("Unauthorized.");

        var conversation = new Conversation
        {
            Type = ConversationType.Group,
            Name = request.Name,
            CreatedBy = currentUserId.Value
        };

        // Add creator as owner
        conversation.Members.Add(new ConversationMember
        {
            Conversation = conversation,
            UserId = currentUserId.Value,
            Role = MemberRole.Owner
        });

        // Add other members
        foreach (var mId in request.MemberIds.Distinct())
        {
            if (mId == currentUserId.Value) continue;

            // Verify they exist
            var userExists = await _unitOfWork.Users.GetByIdAsync(mId, cancellationToken);
            if (userExists == null) continue;

            conversation.Members.Add(new ConversationMember
            {
                Conversation = conversation,
                UserId = mId,
                Role = MemberRole.Member
            });
        }

        await _unitOfWork.Conversations.AddAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _unitOfWork.Conversations.GetByIdAsync(conversation.Id, cancellationToken);
        var dto = _mapper.Map<ConversationDto>(reloaded!);

        // Broadcast to all participants
        var notifyUserIds = conversation.Members.Select(m => m.UserId).Where(uid => uid != currentUserId.Value);
        await _signalRService.SendToUsersAsync(notifyUserIds, "OnConversationCreated", dto, cancellationToken);

        return dto;
    }
}

// Rename Group
public record RenameGroupCommand(Guid ConversationId, string NewName) : IRequest<Result>;

public class RenameGroupValidator : AbstractValidator<RenameGroupCommand>
{
    public RenameGroupValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(200);
    }
}

public class RenameGroupHandler : IRequestHandler<RenameGroupCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;

    public RenameGroupHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
    }

    public async Task<Result> Handle(RenameGroupCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var conversation = await _unitOfWork.Conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null || conversation.Type != ConversationType.Group)
        {
            return Result.Failure("Group conversation not found.");
        }

        var member = conversation.Members.FirstOrDefault(m => m.UserId == currentUserId.Value);
        if (member == null || (member.Role != MemberRole.Owner && member.Role != MemberRole.Admin))
        {
            return Result.Failure("Only owners or administrators can rename the group.");
        }

        conversation.Name = request.NewName;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Conversations.Update(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify
        await _signalRService.SendToConversationAsync(conversation.Id, "OnGroupRenamed", new { ConversationId = conversation.Id, NewName = request.NewName }, cancellationToken);

        return Result.Success();
    }
}

// Add Member
public record AddMemberCommand(Guid ConversationId, Guid MemberId) : IRequest<Result>;

public class AddMemberHandler : IRequestHandler<AddMemberCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;

    public AddMemberHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
    }

    public async Task<Result> Handle(AddMemberCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var conversation = await _unitOfWork.Conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null || conversation.Type != ConversationType.Group)
        {
            return Result.Failure("Group conversation not found.");
        }

        var caller = conversation.Members.FirstOrDefault(m => m.UserId == currentUserId.Value);
        if (caller == null || (caller.Role != MemberRole.Owner && caller.Role != MemberRole.Admin))
        {
            return Result.Failure("Only owners or administrators can add members.");
        }

        var existingMember = conversation.Members.FirstOrDefault(m => m.UserId == request.MemberId);
        if (existingMember != null)
        {
            return Result.Failure("User is already a member of this conversation.");
        }

        var newMember = new ConversationMember
        {
            ConversationId = request.ConversationId,
            UserId = request.MemberId,
            Role = MemberRole.Member
        };

        await _unitOfWork.Conversations.AddMemberAsync(newMember, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify
        await _signalRService.SendToConversationAsync(conversation.Id, "OnMemberAdded", new { ConversationId = conversation.Id, UserId = request.MemberId }, cancellationToken);

        return Result.Success();
    }
}

// Remove Member
public record RemoveMemberCommand(Guid ConversationId, Guid MemberId) : IRequest<Result>;

public class RemoveMemberHandler : IRequestHandler<RemoveMemberCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;

    public RemoveMemberHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
    }

    public async Task<Result> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var conversation = await _unitOfWork.Conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null || conversation.Type != ConversationType.Group)
        {
            return Result.Failure("Group conversation not found.");
        }

        var caller = conversation.Members.FirstOrDefault(m => m.UserId == currentUserId.Value);
        if (caller == null || (caller.Role != MemberRole.Owner && caller.Role != MemberRole.Admin))
        {
            return Result.Failure("Only owners or administrators can remove members.");
        }

        var targetMember = conversation.Members.FirstOrDefault(m => m.UserId == request.MemberId);
        if (targetMember == null)
        {
            return Result.Failure("User is not a member of this conversation.");
        }

        // Owner cannot be removed unless they transfer ownership
        if (targetMember.Role == MemberRole.Owner)
        {
            return Result.Failure("Cannot remove the owner of the group.");
        }

        _unitOfWork.Conversations.RemoveMember(targetMember);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify
        await _signalRService.SendToConversationAsync(conversation.Id, "OnMemberRemoved", new { ConversationId = conversation.Id, UserId = request.MemberId }, cancellationToken);
        await _signalRService.SendToUserAsync(request.MemberId, "OnRemovedFromGroup", new { ConversationId = conversation.Id }, cancellationToken);

        return Result.Success();
    }
}

// Leave Group
public record LeaveGroupCommand(Guid ConversationId) : IRequest<Result>;

public class LeaveGroupHandler : IRequestHandler<LeaveGroupCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;

    public LeaveGroupHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
    }

    public async Task<Result> Handle(LeaveGroupCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var conversation = await _unitOfWork.Conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null || conversation.Type != ConversationType.Group)
        {
            return Result.Failure("Group conversation not found.");
        }

        var member = conversation.Members.FirstOrDefault(m => m.UserId == currentUserId.Value);
        if (member == null)
        {
            return Result.Failure("You are not a member of this group.");
        }

        // If Owner leaves, assign Owner role to next member
        if (member.Role == MemberRole.Owner && conversation.Members.Count > 1)
        {
            var nextOwner = conversation.Members
                .Where(m => m.UserId != currentUserId.Value)
                .OrderBy(m => m.Role == MemberRole.Admin ? 0 : 1)
                .ThenBy(m => m.JoinedAt)
                .First();

            nextOwner.Role = MemberRole.Owner;
            _unitOfWork.Conversations.UpdateMember(nextOwner);
        }

        _unitOfWork.Conversations.RemoveMember(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify
        await _signalRService.SendToConversationAsync(conversation.Id, "OnMemberLeft", new { ConversationId = conversation.Id, UserId = currentUserId.Value }, cancellationToken);

        return Result.Success();
    }
}

// Get Conversations list
public record GetConversationsQuery(
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedList<ConversationDto>>>;

public class GetConversationsHandler : IRequestHandler<GetConversationsQuery, Result<PagedList<ConversationDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetConversationsHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<PagedList<ConversationDto>>> Handle(GetConversationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<PagedList<ConversationDto>>("Unauthorized.");

        var conversations = await _unitOfWork.Conversations.GetConversationsForUserAsync(userId.Value, request.PageNumber, request.PageSize, cancellationToken);
        var dtos = new List<ConversationDto>();

        foreach (var c in conversations.Items)
        {
            var userMember = c.Members.First(m => m.UserId == userId.Value);
            var isMuted = userMember.IsMuted;
            
            // Calculate unread count (messages sent after last_read_at, excluding sender_id == userId)
            var lastRead = userMember.LastReadAt ?? DateTimeOffset.MinValue;
            var unreadCount = c.Messages.Count(m => m.CreatedAt > lastRead && m.SenderId != userId.Value);

            var lastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

            // Direct chat name and avatar is resolved by the OTHER user
            string? name = c.Name;
            string? avatarUrl = c.AvatarUrl;

            if (c.Type == ConversationType.Direct)
            {
                var otherMember = c.Members.FirstOrDefault(m => m.UserId != userId.Value);
                if (otherMember != null)
                {
                    name = otherMember.User.DisplayName;
                    avatarUrl = otherMember.User.AvatarUrl;
                }
            }

            var mappedMembers = _mapper.Map<List<ConversationMemberDto>>(c.Members);

            dtos.Add(new ConversationDto(
                c.Id,
                c.Type,
                name,
                avatarUrl,
                c.CreatedBy,
                c.CreatedAt,
                c.UpdatedAt,
                mappedMembers,
                isMuted,
                unreadCount,
                lastMessage?.IsDeleted == true ? "This message was deleted." : lastMessage?.TextContent,
                lastMessage?.CreatedAt
            ));
        }

        return new PagedList<ConversationDto>(dtos, conversations.TotalCount, conversations.PageNumber, conversations.PageSize);
    }
}

// Mapping Profiles
public class ConversationMappingProfile : Profile
{
    public ConversationMappingProfile()
    {
        CreateMap<ConversationMember, ConversationMemberDto>();
        CreateMap<Conversation, ConversationDto>()
            .ForCtorParam("IsMuted", opt => opt.MapFrom(src => false))
            .ForCtorParam("UnreadCount", opt => opt.MapFrom(src => 0))
            .ForCtorParam("LastMessageText", opt => opt.MapFrom(src => (string?)null))
            .ForCtorParam("LastMessageTime", opt => opt.MapFrom(src => (DateTimeOffset?)null));
    }
}
