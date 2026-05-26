using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using MediatR;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Application.Common.Models;
using CyberChat.Application.Features.Auth;
using CyberChat.Domain.Entities;

namespace CyberChat.Application.Features.Users;

// Get Profile Feature
public record GetProfileQuery(Guid UserId) : IRequest<Result<UserDto>>;

public class GetProfileHandler : IRequestHandler<GetProfileQuery, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetProfileHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<UserDto>> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<UserDto>("User not found.");
        }

        return _mapper.Map<UserDto>(user);
    }
}

// Update Profile Feature
public record UpdateProfileCommand(
    string DisplayName,
    string? Bio,
    string? CoverUrl
) : IRequest<Result<UserDto>>;

public class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Bio).MaximumLength(500);
        RuleFor(x => x.CoverUrl).MaximumLength(1000);
    }
}

public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public UpdateProfileHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<UserDto>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<UserDto>("Unauthorized.");

        var user = await _unitOfWork.Users.GetByIdAsync(userId.Value, cancellationToken);
        if (user == null) return Result.Failure<UserDto>("User not found.");

        user.DisplayName = request.DisplayName;
        user.Bio = request.Bio;
        if (request.CoverUrl != null)
        {
            user.CoverUrl = request.CoverUrl;
        }
        
        user.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<UserDto>(user);
    }
}

// Upload Avatar Feature
public record UploadAvatarCommand(
    Stream FileStream,
    string FileName,
    string ContentType
) : IRequest<Result<string>>;

public class UploadAvatarHandler : IRequestHandler<UploadAvatarCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStorageService _storageService;

    public UploadAvatarHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IStorageService storageService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _storageService = storageService;
    }

    public async Task<Result<string>> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result.Failure<string>("Unauthorized.");

        var user = await _unitOfWork.Users.GetByIdAsync(userId.Value, cancellationToken);
        if (user == null) return Result.Failure<string>("User not found.");

        // Upload new avatar using the Storage Service
        var uniqueFileName = $"avatars/{userId}/{Guid.NewGuid()}_{request.FileName}";
        var avatarUrl = await _storageService.UploadFileAsync("cyberchat-media", uniqueFileName, request.FileStream, request.ContentType, cancellationToken);

        // Update user avatar reference
        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(avatarUrl);
    }
}

// Search Users Feature
public record SearchUsersQuery(
    string? Query,
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedList<UserDto>>>;

public class SearchUsersHandler : IRequestHandler<SearchUsersQuery, Result<PagedList<UserDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SearchUsersHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<PagedList<UserDto>>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        var pagedUsers = await _unitOfWork.Users.SearchUsersAsync(request.Query, request.PageNumber, request.PageSize, cancellationToken);
        
        var dtoList = _mapper.Map<List<UserDto>>(pagedUsers.Items);
        var pagedResult = new PagedList<UserDto>(dtoList, pagedUsers.TotalCount, pagedUsers.PageNumber, pagedUsers.PageSize);

        return pagedResult;
    }
}
