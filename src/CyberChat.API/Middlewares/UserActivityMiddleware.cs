using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using CyberChat.Application.Common.Interfaces;

namespace CyberChat.API.Middlewares;

public class UserActivityMiddleware
{
    private readonly RequestDelegate _next;

    public UserActivityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Run after the request finishes to avoid blocking the API response
        var userIdStr = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdStr, out var userId))
        {
            try
            {
                var cacheKey = $"activity:throttle:{userId}";
                var cache = context.RequestServices.GetRequiredService<ICacheService>();

                // Check Redis cache to throttle DB writes to once every 5 minutes
                var isThrottled = await cache.GetAsync<string>(cacheKey);
                if (isThrottled == null)
                {
                    // Set throttle in Redis
                    await cache.SetAsync(cacheKey, "active", TimeSpan.FromMinutes(5));

                    // Run DB update
                    using (var scope = context.RequestServices.CreateScope())
                    {
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        var sessions = await unitOfWork.UserSessions.GetByRefreshTokenAsync("", default); // not matching token, but let's query user latest session or write custom SQL
                        
                        // Let's do a simple context update for user session
                        // Since we don't have token in claim, we can fetch all sessions of this user and update the last_active_at of the most recent one
                        var user = await unitOfWork.Users.GetByIdAsync(userId);
                        if (user != null)
                        {
                            var latestSession = user.Sessions.OrderByDescending(s => s.LastActiveAt).FirstOrDefault();
                            if (latestSession != null)
                            {
                                latestSession.LastActiveAt = DateTimeOffset.UtcNow;
                                await unitOfWork.SaveChangesAsync();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silence activity update errors to not crash the request lifecycle
            }
        }
    }
}
