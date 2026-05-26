using System;
using System.Collections.Generic;
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

namespace CyberChat.Application.Features.Friends;

// DTOs
public record FriendRequestDto(
    Guid Id,
    UserDto FromUser,
    UserDto ToUser,
    FriendRequestStatus Status,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

// Send Friend Request
public record SendFriendRequestCommand(
    Guid ToUserId,
    string? Message
) : IRequest<Result<FriendRequestDto>>;

public class SendFriendRequestValidator : AbstractValidator<SendFriendRequestCommand>
{
    public SendFriendRequestValidator()
    {
        RuleFor(x => x.ToUserId).NotEmpty();
        RuleFor(x => x.Message).MaximumLength(500);
    }
}

public class SendFriendRequestHandler : IRequestHandler<SendFriendRequestCommand, Result<FriendRequestDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;
    private readonly IMapper _mapper;

    public SendFriendRequestHandler(
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

    public async Task<Result<FriendRequestDto>> Handle(SendFriendRequestCommand request, CancellationToken cancellationToken)
    {
        var fromUserId = _currentUserService.UserId;
        if (fromUserId == null) return Result.Failure<FriendRequestDto>("Unauthorized.");
        if (fromUserId == request.ToUserId) return Result.Failure<FriendRequestDto>("Cannot send friend request to yourself.");

        var toUser = await _unitOfWork.Users.GetByIdAsync(request.ToUserId, cancellationToken);
        if (toUser == null) return Result.Failure<FriendRequestDto>("Recipient user not found.");

        // Check blocking
        if (await _unitOfWork.Friends.IsBlockedAsync(fromUserId.Value, request.ToUserId, cancellationToken) ||
            await _unitOfWork.Friends.IsBlockedAsync(request.ToUserId, fromUserId.Value, cancellationToken))
        {
            return Result.Failure<FriendRequestDto>("Cannot send request. Blocking relation exists.");
        }

        // Check if friendship already exists
        var existingFriendship = await _unitOfWork.Friends.GetFriendshipAsync(fromUserId.Value, request.ToUserId, cancellationToken);
        if (existingFriendship != null)
        {
            return Result.Failure<FriendRequestDto>("You are already friends.");
        }

        // Check existing request
        var existingRequest = await _unitOfWork.Friends.GetRequestAsync(fromUserId.Value, request.ToUserId, cancellationToken);
        if (existingRequest != null && existingRequest.Status == FriendRequestStatus.Pending)
        {
            return Result.Failure<FriendRequestDto>("Friend request is already pending.");
        }

        var incomingRequest = await _unitOfWork.Friends.GetRequestAsync(request.ToUserId, fromUserId.Value, cancellationToken);
        if (incomingRequest != null && incomingRequest.Status == FriendRequestStatus.Pending)
        {
            return Result.Failure<FriendRequestDto>("You already have a pending request from this user. Accept that instead.");
        }

        // Handle case where request was rejected/cancelled before, we can just reset it or create a new one
        if (existingRequest != null)
        {
            existingRequest.Status = FriendRequestStatus.Pending;
            existingRequest.Message = request.Message;
            existingRequest.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Friends.RemoveRequest(existingRequest); // Remove to re-insert or update
        }

        var friendRequest = new FriendRequest
        {
            FromUserId = fromUserId.Value,
            ToUserId = request.ToUserId,
            Status = FriendRequestStatus.Pending,
            Message = request.Message
        };

        await _unitOfWork.Friends.AddRequestAsync(friendRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Load complete sender information for DTO mapping
        var fromUser = await _unitOfWork.Users.GetByIdAsync(fromUserId.Value, cancellationToken);
        friendRequest.FromUser = fromUser!;
        friendRequest.ToUser = toUser;

        var dto = _mapper.Map<FriendRequestDto>(friendRequest);

        // Realtime notification
        await _signalRService.SendToUserAsync(request.ToUserId, "OnFriendRequestReceived", dto, cancellationToken);

        return dto;
    }
}

// Accept Friend Request
public record AcceptFriendRequestCommand(Guid RequestId) : IRequest<Result>;

public class AcceptFriendRequestHandler : IRequestHandler<AcceptFriendRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;

    public AcceptFriendRequestHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISignalRService signalRService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
    }

    public async Task<Result> Handle(AcceptFriendRequestCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var friendRequest = await _unitOfWork.Friends.GetRequestByIdAsync(request.RequestId, cancellationToken);
        if (friendRequest == null || friendRequest.ToUserId != currentUserId.Value || friendRequest.Status != FriendRequestStatus.Pending)
        {
            return Result.Failure("Pending friend request not found.");
        }

        friendRequest.Status = FriendRequestStatus.Accepted;
        friendRequest.UpdatedAt = DateTimeOffset.UtcNow;

        // Establish Friendship: Order User IDs (User A must be < User B)
        var userAId = friendRequest.FromUserId < friendRequest.ToUserId ? friendRequest.FromUserId : friendRequest.ToUserId;
        var userBId = friendRequest.FromUserId < friendRequest.ToUserId ? friendRequest.ToUserId : friendRequest.FromUserId;

        var friendship = new Friendship
        {
            UserAId = userAId,
            UserBId = userBId
        };

        await _unitOfWork.Friends.AddFriendshipAsync(friendship, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify both users in real-time
        await _signalRService.SendToUserAsync(friendRequest.FromUserId, "OnFriendRequestAccepted", new { RequestId = friendRequest.Id, FriendId = friendRequest.ToUserId }, cancellationToken);
        await _signalRService.SendToUserAsync(friendRequest.ToUserId, "OnFriendRequestAccepted", new { RequestId = friendRequest.Id, FriendId = friendRequest.FromUserId }, cancellationToken);

        return Result.Success();
    }
}

// Reject Friend Request
public record RejectFriendRequestCommand(Guid RequestId) : IRequest<Result>;

public class RejectFriendRequestHandler : IRequestHandler<RejectFriendRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RejectFriendRequestHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RejectFriendRequestCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var friendRequest = await _unitOfWork.Friends.GetRequestByIdAsync(request.RequestId, cancellationToken);
        if (friendRequest == null || friendRequest.ToUserId != currentUserId.Value || friendRequest.Status != FriendRequestStatus.Pending)
        {
            return Result.Failure("Pending friend request not found.");
        }

        friendRequest.Status = FriendRequestStatus.Rejected;
        friendRequest.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// Cancel Friend Request
public record CancelFriendRequestCommand(Guid RequestId) : IRequest<Result>;

public class CancelFriendRequestHandler : IRequestHandler<CancelFriendRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public CancelFriendRequestHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(CancelFriendRequestCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var friendRequest = await _unitOfWork.Friends.GetRequestByIdAsync(request.RequestId, cancellationToken);
        if (friendRequest == null || friendRequest.FromUserId != currentUserId.Value || friendRequest.Status != FriendRequestStatus.Pending)
        {
            return Result.Failure("Pending friend request not found.");
        }

        _unitOfWork.Friends.RemoveRequest(friendRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// Remove Friend
public record RemoveFriendCommand(Guid FriendId) : IRequest<Result>;

public class RemoveFriendHandler : IRequestHandler<RemoveFriendCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISignalRService _signalRService;

    public RemoveFriendHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, ISignalRService signalRService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _signalRService = signalRService;
    }

    public async Task<Result> Handle(RemoveFriendCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Failure("Unauthorized.");

        var friendship = await _unitOfWork.Friends.GetFriendshipAsync(currentUserId.Value, request.FriendId, cancellationToken);
        if (friendship == null)
        {
            return Result.Failure("You are not friends with this user.");
        }

        _unitOfWork.Friends.RemoveFriendship(friendship);

        // Delete any historical requests
        var req1 = await _unitOfWork.Friends.GetRequestAsync(currentUserId.Value, request.FriendId, cancellationToken);
        if (req1 != null) _unitOfWork.Friends.RemoveRequest(req1);
        var req2 = await _unitOfWork.Friends.GetRequestAsync(request.FriendId, currentUserId.Value, cancellationToken);
        if (req2 != null) _unitOfWork.Friends.RemoveRequest(req2);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify
        await _signalRService.SendToUserAsync(request.FriendId, "OnFriendshipRemoved", new { FriendId = currentUserId.Value }, cancellationToken);
        await _signalRService.SendToUserAsync(currentUserId.Value, "OnFriendshipRemoved", new { FriendId = request.FriendId }, cancellationToken);

        return Result.Success();
    }
}

// Block User
public record BlockUserCommand(Guid BlockedId) : IRequest<Result>;

public class BlockUserHandler : IRequestHandler<BlockUserCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public BlockUserHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(BlockUserCommand request, CancellationToken cancellationToken)
    {
        var blockerId = _currentUserService.UserId;
        if (blockerId == null) return Result.Failure("Unauthorized.");
        if (blockerId.Value == request.BlockedId) return Result.Failure("Cannot block yourself.");

        var block = await _unitOfWork.Friends.GetBlockAsync(blockerId.Value, request.BlockedId, cancellationToken);
        if (block != null) return Result.Failure("User is already blocked.");

        // Remove friendship if exists
        var friendship = await _unitOfWork.Friends.GetFriendshipAsync(blockerId.Value, request.BlockedId, cancellationToken);
        if (friendship != null)
        {
            _unitOfWork.Friends.RemoveFriendship(friendship);
        }

        // Clean up requests
        var req1 = await _unitOfWork.Friends.GetRequestAsync(blockerId.Value, request.BlockedId, cancellationToken);
        if (req1 != null) _unitOfWork.Friends.RemoveRequest(req1);
        var req2 = await _unitOfWork.Friends.GetRequestAsync(request.BlockedId, blockerId.Value, cancellationToken);
        if (req2 != null) _unitOfWork.Friends.RemoveRequest(req2);

        var newBlock = new Block
        {
            BlockerId = blockerId.Value,
            BlockedId = request.BlockedId
        };

        await _unitOfWork.Friends.AddBlockAsync(newBlock, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// Unblock User
public record UnblockUserCommand(Guid BlockedId) : IRequest<Result>;

public class UnblockUserHandler : IRequestHandler<UnblockUserCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UnblockUserHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(UnblockUserCommand request, CancellationToken cancellationToken)
    {
        var blockerId = _currentUserService.UserId;
        if (blockerId == null) return Result.Failure("Unauthorized.");

        var block = await _unitOfWork.Friends.GetBlockAsync(blockerId.Value, request.BlockedId, cancellationToken);
        if (block == null) return Result.Failure("Block relationship does not exist.");

        _unitOfWork.Friends.RemoveBlock(block);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// Get Friends List
public record GetFriendsQuery : IRequest<Result<List<UserDto>>>;

public class GetFriendsHandler : IRequestHandler<GetFriendsQuery, Result<List<UserDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetFriendsHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<List<UserDto>>> Handle(GetFriendsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<List<UserDto>>("Unauthorized.");

        var friends = await _unitOfWork.Friends.GetFriendsAsync(userId.Value, cancellationToken);
        return _mapper.Map<List<UserDto>>(friends);
    }
}

// Get Pending Requests
public record GetPendingRequestsQuery : IRequest<Result<List<FriendRequestDto>>>;

public class GetPendingRequestsHandler : IRequestHandler<GetPendingRequestsQuery, Result<List<FriendRequestDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetPendingRequestsHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<List<FriendRequestDto>>> Handle(GetPendingRequestsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<List<FriendRequestDto>>("Unauthorized.");

        var requests = await _unitOfWork.Friends.GetPendingRequestsAsync(userId.Value, cancellationToken);
        return _mapper.Map<List<FriendRequestDto>>(requests);
    }
}

// Get Blocked Users
public record GetBlockedUsersQuery : IRequest<Result<List<UserDto>>>;

public class GetBlockedUsersHandler : IRequestHandler<GetBlockedUsersQuery, Result<List<UserDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetBlockedUsersHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<List<UserDto>>> Handle(GetBlockedUsersQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<List<UserDto>>("Unauthorized.");

        var blocked = await _unitOfWork.Friends.GetBlockedUsersAsync(userId.Value, cancellationToken);
        return _mapper.Map<List<UserDto>>(blocked);
    }
}

// AutoMapper Profile
public class FriendsMappingProfile : Profile
{
    public FriendsMappingProfile()
    {
        CreateMap<FriendRequest, FriendRequestDto>();
    }
}
