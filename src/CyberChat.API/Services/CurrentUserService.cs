using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using CyberChat.Application.Common.Interfaces;

namespace CyberChat.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var userIdStr = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdStr, out var parsedId))
            {
                return parsedId;
            }
            return null;
        }
    }

    public bool IsAuthenticated => UserId.HasValue;
}
