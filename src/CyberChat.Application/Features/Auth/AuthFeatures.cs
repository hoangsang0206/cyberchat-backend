using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using MediatR;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Application.Common.Models;
using CyberChat.Domain.Entities;
using CyberChat.Domain.Enums;

namespace CyberChat.Application.Features.Auth;

// DTOs
public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Uid,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? CoverUrl,
    string? Email,
    string? Phone,
    bool IsActive,
    DateTimeOffset CreatedAt
);

// Register Feature
public record RegisterCommand(
    string Uid,
    string DisplayName,
    string Email,
    string? Phone,
    string Password
) : IRequest<Result<AuthResponseDto>>;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Uid)
            .NotEmpty()
            .Matches("^[a-zA-Z0-9_.]{3,32}$")
            .WithMessage("UID must be 3-32 characters and contain only letters, numbers, dots, or underscores.");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class RegisterHandler : IRequestHandler<RegisterCommand, Result<AuthResponseDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtProvider _jwtProvider;
    private readonly IMapper _mapper;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterHandler(IUnitOfWork unitOfWork, IJwtProvider jwtProvider, IMapper mapper, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _jwtProvider = jwtProvider;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AuthResponseDto>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _unitOfWork.Users.ExistsByUidAsync(request.Uid, cancellationToken))
        {
            return Result.Failure<AuthResponseDto>("UID is already taken.");
        }

        if (await _unitOfWork.Users.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            return Result.Failure<AuthResponseDto>("Email is already registered.");
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Uid = request.Uid,
            DisplayName = request.DisplayName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = passwordHash
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        
        // Setup a default session
        var refreshToken = _jwtProvider.GenerateRefreshToken();
        var session = new UserSession
        {
            UserId = user.Id,
            DeviceName = "Web",
            DeviceType = DeviceType.Web,
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            User = user
        };
        await _unitOfWork.UserSessions.AddAsync(session, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtProvider.GenerateAccessToken(user);
        var userDto = _mapper.Map<UserDto>(user);

        return new AuthResponseDto(accessToken, refreshToken, userDto);
    }
}

// Login Feature
public record LoginCommand(
    string EmailOrUid,
    string Password,
    string? DeviceName,
    DeviceType DeviceType,
    string? IpAddress
) : IRequest<Result<AuthResponseDto>>;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.EmailOrUid).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtProvider _jwtProvider;
    private readonly IMapper _mapper;
    private readonly IPasswordHasher _passwordHasher;

    public LoginHandler(IUnitOfWork unitOfWork, IJwtProvider jwtProvider, IMapper mapper, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _jwtProvider = jwtProvider;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AuthResponseDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        User? user = null;
        if (request.EmailOrUid.Contains("@"))
        {
            user = await _unitOfWork.Users.GetByEmailAsync(request.EmailOrUid, cancellationToken);
        }
        else
        {
            user = await _unitOfWork.Users.GetByUidAsync(request.EmailOrUid, cancellationToken);
        }

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResponseDto>("Invalid credentials.");
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponseDto>("User account is inactive.");
        }

        var accessToken = _jwtProvider.GenerateAccessToken(user);
        var refreshToken = _jwtProvider.GenerateRefreshToken();

        IPAddress? ip = null;
        if (!string.IsNullOrEmpty(request.IpAddress))
        {
            IPAddress.TryParse(request.IpAddress, out ip);
        }

        var session = new UserSession
        {
            UserId = user.Id,
            DeviceName = request.DeviceName ?? "Unknown Device",
            DeviceType = request.DeviceType,
            IpAddress = ip,
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            User = user
        };

        // Clean up expired sessions first
        _unitOfWork.UserSessions.DeleteExpiredSessions(user.Id);
        await _unitOfWork.UserSessions.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = _mapper.Map<UserDto>(user);

        return new AuthResponseDto(accessToken, refreshToken, userDto);
    }
}

// Refresh Token Feature
public record RefreshTokenCommand(
    string RefreshToken,
    string? IpAddress
) : IRequest<Result<AuthResponseDto>>;

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtProvider _jwtProvider;
    private readonly IMapper _mapper;

    public RefreshTokenHandler(IUnitOfWork unitOfWork, IJwtProvider jwtProvider, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _jwtProvider = jwtProvider;
        _mapper = mapper;
    }

    public async Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var session = await _unitOfWork.UserSessions.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (session == null || session.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Result.Failure<AuthResponseDto>("Invalid or expired refresh token.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(session.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return Result.Failure<AuthResponseDto>("User account is inactive or not found.");
        }

        // Generate new tokens
        var newAccessToken = _jwtProvider.GenerateAccessToken(user);
        var newRefreshToken = _jwtProvider.GenerateRefreshToken();

        // Refresh token rotation: update existing session with new token and active stamp
        session.RefreshToken = newRefreshToken;
        session.ExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
        session.LastActiveAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(request.IpAddress) && IPAddress.TryParse(request.IpAddress, out var ip))
        {
            session.IpAddress = ip;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = _mapper.Map<UserDto>(user);
        return new AuthResponseDto(newAccessToken, newRefreshToken, userDto);
    }
}

// Logout Feature
public record LogoutCommand(string RefreshToken) : IRequest<Result>;

public class LogoutHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;

    public LogoutHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var session = await _unitOfWork.UserSessions.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (session != null)
        {
            _unitOfWork.UserSessions.Delete(session);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

// Get Current User Feature
public record GetCurrentUserQuery : IRequest<Result<UserDto>>;

public class GetCurrentUserHandler : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetCurrentUserHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            return Result.Failure<UserDto>("Unauthorized access.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId.Value, cancellationToken);
        if (user == null)
        {
            return Result.Failure<UserDto>("User not found.");
        }

        return _mapper.Map<UserDto>(user);
    }
}

// AutoMapper Profile
public class AuthMappingProfile : Profile
{
    public AuthMappingProfile()
    {
        CreateMap<User, UserDto>();
    }
}
