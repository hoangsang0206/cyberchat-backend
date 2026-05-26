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

namespace CyberChat.Application.Features.Messages;

// DTOs
public record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    UserDto Sender,
    MessageContentType ContentType,
    string? TextContent,
    Guid? ReplyToId,
    MessageReplyDto? ReplyTo,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<MessageAttachmentDto> Attachments,
    List<MessageReactionDto> Reactions
);

public record MessageReplyDto(
    Guid Id,
    Guid SenderId,
    string DisplayName,
    MessageContentType ContentType,
    string? TextContent
);

public record MessageAttachmentDto(
    Guid Id,
    AttachmentType Type,
    string Url,
    string? FileName,
    long? FileSize,
    string? MimeType,
    int? Width,
    int? Height,
    int? DurationMs,
    string? StickerPackId
);

public record MessageReactionDto(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string Emoji
);

public record AttachmentInput(
    AttachmentType Type,
    string Url,
    string? FileName,
    long? FileSize,
    string? MimeType,
    int? Width,
    int? Height,
    int? DurationMs,
    string? StickerPackId
);

// Send Message (Supports direct & reply by referencing ReplyToId)
public record SendMessageCommand(
    Guid ConversationId,
    MessageContentType ContentType,
    string? TextContent,
    Guid? ReplyToId,
    List<AttachmentInput>? Attachments
) : IRequest<Result<MessageDto>>;

public class SendMessageValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.ContentType).IsInEnum();
        RuleFor(x => x.TextContent)
            .NotEmpty()
            .When(x => x.ContentType == MessageContentType.Text && (x.Attachments == null || x.Attachments.Count == 0))
            .WithMessage("Message text content cannot be empty.");
    }
}

public class SendMessageHandler : IRequestHandler<SendMessageCommand, Result<MessageDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;
    private readonly IMapper _mapper;

    public SendMessageHandler(
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

    public async Task<Result<MessageDto>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var senderId = _currentUserService.UserId;
        if (senderId == null) return Result.Failure<MessageDto>("Unauthorized.");

        var conversation = await _unitOfWork.Conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null) return Result.Failure<MessageDto>("Conversation not found.");

        var isMember = conversation.Members.Any(m => m.UserId == senderId.Value);
        if (!isMember) return Result.Failure<MessageDto>("You are not a member of this conversation.");

        var message = new Message
        {
            ConversationId = request.ConversationId,
            SenderId = senderId.Value,
            ContentType = request.ContentType,
            TextContent = request.TextContent,
            ReplyToId = request.ReplyToId,
        };

        if (request.Attachments != null)
        {
            foreach (var attach in request.Attachments)
            {
                message.Attachments.Add(new MessageAttachment
                {
                    Type = attach.Type,
                    Url = attach.Url,
                    FileName = attach.FileName,
                    FileSize = attach.FileSize,
                    MimeType = attach.MimeType,
                    Width = attach.Width,
                    Height = attach.Height,
                    DurationMs = attach.DurationMs,
                    StickerPackId = attach.StickerPackId
                });
            }
        }

        // Update conversation touch time
        conversation.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.Messages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload message to fetch Sender and ReplyTo associations
        var reloaded = await _unitOfWork.Messages.GetByIdAsync(message.Id, cancellationToken);
        var dto = _mapper.Map<MessageDto>(reloaded!);

        // Send realtime event to all conversation participants
        var memberUserIds = conversation.Members.Select(m => m.UserId);
        await _signalRService.SendToUsersAsync(memberUserIds, "OnMessageReceived", dto, cancellationToken);

        return dto;
    }
}

// Edit Message
public record EditMessageCommand(Guid MessageId, string NewContent) : IRequest<Result<MessageDto>>;

public class EditMessageValidator : AbstractValidator<EditMessageCommand>
{
    public EditMessageValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.NewContent).NotEmpty();
    }
}

public class EditMessageHandler : IRequestHandler<EditMessageCommand, Result<MessageDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;
    private readonly IMapper _mapper;

    public EditMessageHandler(
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

    public async Task<Result<MessageDto>> Handle(EditMessageCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure<MessageDto>("Unauthorized.");

        var message = await _unitOfWork.Messages.GetByIdAsync(request.MessageId, cancellationToken);
        if (message == null || message.IsDeleted) return Result.Failure<MessageDto>("Message not found.");

        if (message.SenderId != currentUserId.Value)
        {
            return Result.Failure<MessageDto>("You can only edit your own messages.");
        }

        message.TextContent = request.NewContent;
        message.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Messages.Update(message);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<MessageDto>(message);

        // Broadcast to conversation
        await _signalRService.SendToConversationAsync(message.ConversationId, "OnMessageEdited", dto, cancellationToken);

        return dto;
    }
}

// Delete Message
public record DeleteMessageCommand(Guid MessageId) : IRequest<Result>;

public class DeleteMessageHandler : IRequestHandler<DeleteMessageCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;

    public DeleteMessageHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
    }

    public async Task<Result> Handle(DeleteMessageCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var message = await _unitOfWork.Messages.GetByIdAsync(request.MessageId, cancellationToken);
        if (message == null || message.IsDeleted) return Result.Failure("Message not found.");

        // Check if sender or group owner/admin
        var hasAccess = message.SenderId == currentUserId.Value;
        if (!hasAccess)
        {
            var conversation = await _unitOfWork.Conversations.GetByIdAsync(message.ConversationId, cancellationToken);
            var member = conversation?.Members.FirstOrDefault(m => m.UserId == currentUserId.Value);
            if (member != null && (member.Role == MemberRole.Owner || member.Role == MemberRole.Admin))
            {
                hasAccess = true;
            }
        }

        if (!hasAccess)
        {
            return Result.Failure("You do not have permission to delete this message.");
        }

        // Soft delete message
        message.IsDeleted = true;
        message.TextContent = null;
        message.UpdatedAt = DateTimeOffset.UtcNow;

        // Clear attachments from DB (optional, but clean)
        message.Attachments.Clear();

        _unitOfWork.Messages.Update(message);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Broadcast deleted state
        await _signalRService.SendToConversationAsync(message.ConversationId, "OnMessageDeleted", new { MessageId = message.Id }, cancellationToken);

        return Result.Success();
    }
}

// React to Message
public record ReactToMessageCommand(Guid MessageId, string Emoji) : IRequest<Result>;

public class ReactToMessageValidator : AbstractValidator<ReactToMessageCommand>
{
    public ReactToMessageValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.Emoji).NotEmpty().MaximumLength(10);
    }
}

public class ReactToMessageHandler : IRequestHandler<ReactToMessageCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;

    public ReactToMessageHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
    }

    public async Task<Result> Handle(ReactToMessageCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var message = await _unitOfWork.Messages.GetByIdAsync(request.MessageId, cancellationToken);
        if (message == null || message.IsDeleted) return Result.Failure("Message not found.");

        var existingReaction = await _unitOfWork.Messages.GetReactionAsync(request.MessageId, currentUserId.Value, request.Emoji, cancellationToken);

        if (existingReaction != null)
        {
            // Toggle reaction: remove if already exists
            _unitOfWork.Messages.RemoveReaction(existingReaction);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _signalRService.SendToConversationAsync(message.ConversationId, "OnMessageReacted", new
            {
                MessageId = message.Id,
                UserId = currentUserId.Value,
                Emoji = request.Emoji,
                Action = "Removed"
            }, cancellationToken);
        }
        else
        {
            // Add reaction
            var reaction = new MessageReaction
            {
                MessageId = request.MessageId,
                UserId = currentUserId.Value,
                Emoji = request.Emoji
            };

            await _unitOfWork.Messages.AddReactionAsync(reaction, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var user = await _unitOfWork.Users.GetByIdAsync(currentUserId.Value, cancellationToken);

            await _signalRService.SendToConversationAsync(message.ConversationId, "OnMessageReacted", new
            {
                MessageId = message.Id,
                UserId = currentUserId.Value,
                DisplayName = user?.DisplayName ?? "User",
                Emoji = request.Emoji,
                Action = "Added"
            }, cancellationToken);
        }

        return Result.Success();
    }
}

// Get Messages paginated
public record GetMessagesQuery(
    Guid ConversationId,
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedList<MessageDto>>>;

public class GetMessagesHandler : IRequestHandler<GetMessagesQuery, Result<PagedList<MessageDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetMessagesHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<PagedList<MessageDto>>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure<PagedList<MessageDto>>("Unauthorized.");

        var conversation = await _unitOfWork.Conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null) return Result.Failure<PagedList<MessageDto>>("Conversation not found.");

        var isMember = conversation.Members.Any(m => m.UserId == currentUserId.Value);
        if (!isMember) return Result.Failure<PagedList<MessageDto>>("Access denied.");

        var pagedMessages = await _unitOfWork.Messages.GetMessagesAsync(request.ConversationId, request.PageNumber, request.PageSize, cancellationToken);

        var dtos = _mapper.Map<List<MessageDto>>(pagedMessages.Items);
        
        // Reverse so they are returned in chronological order for client application
        dtos.Reverse();

        return new PagedList<MessageDto>(dtos, pagedMessages.TotalCount, pagedMessages.PageNumber, pagedMessages.PageSize);
    }
}

// AutoMapper Profile
public class MessageMappingProfile : Profile
{
    public MessageMappingProfile()
    {
        CreateMap<Message, MessageDto>();
        CreateMap<MessageAttachment, MessageAttachmentDto>();
        CreateMap<MessageReaction, MessageReactionDto>()
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.User.DisplayName));
        CreateMap<Message, MessageReplyDto>()
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.Sender.DisplayName));
    }
}
