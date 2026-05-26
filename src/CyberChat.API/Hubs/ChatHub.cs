using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Domain.Enums;

namespace CyberChat.API.Hubs;

public class ChatHub : Hub
{
    private readonly IServiceProvider _serviceProvider;

    public ChatHub(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var token = httpContext?.Request.Query["access_token"].ToString();

        if (string.IsNullOrEmpty(token))
        {
            Context.Abort();
            return;
        }

        using (var scope = _serviceProvider.CreateScope())
        {
            var jwtProvider = scope.ServiceProvider.GetRequiredService<IJwtProvider>();
            var userId = jwtProvider.GetUserIdFromToken(token);

            if (userId == null)
            {
                Context.Abort();
                return;
            }

            Context.Items["UserId"] = userId.Value.ToString();

            // 1. Add connection to user-specific group (e.g. user_{userId})
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            // 2. Add connection to all conversations this user is member of
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var conversations = await unitOfWork.Conversations.GetConversationsForUserAsync(userId.Value, 1, 100);
            foreach (var conv in conversations.Items)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conv.Id}");
            }

            // 3. Mark user as online in cache
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await cache.SetUserOnlineAsync(userId.Value, Context.ConnectionId);

            // 4. Broadcast online presence to friends
            var friends = await unitOfWork.Friends.GetFriendsAsync(userId.Value);
            var friendIds = friends.Select(f => f.Id);
            var signalRService = scope.ServiceProvider.GetRequiredService<ISignalRService>();
            await signalRService.SendToUsersAsync(friendIds, "OnUserPresenceChanged", new { UserId = userId.Value, IsOnline = true });
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("UserId", out var userIdStr) && Guid.TryParse(userIdStr?.ToString(), out var userId))
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                await cache.SetUserOfflineAsync(userId, Context.ConnectionId);

                // Check if user is fully offline (no other active connections)
                var isOnline = await cache.IsUserOnlineAsync(userId);
                if (!isOnline)
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var friends = await unitOfWork.Friends.GetFriendsAsync(userId);
                    var friendIds = friends.Select(f => f.Id);
                    
                    var signalRService = scope.ServiceProvider.GetRequiredService<ISignalRService>();
                    await signalRService.SendToUsersAsync(friendIds, "OnUserPresenceChanged", new { UserId = userId, IsOnline = false });
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendTypingStatus(Guid conversationId, bool isTyping)
    {
        if (Context.Items.TryGetValue("UserId", out var userIdStr) && Guid.TryParse(userIdStr?.ToString(), out var userId))
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                await cache.SetUserTypingAsync(conversationId, userId, isTyping);

                // Broadcast typing status to conversation group (excluding sender)
                await Clients.OthersInGroup($"conversation_{conversationId}").SendAsync("OnUserTyping", new { ConversationId = conversationId, UserId = userId, IsTyping = isTyping });
            }
        }
    }

    public async Task MarkAsRead(Guid conversationId)
    {
        if (Context.Items.TryGetValue("UserId", out var userIdStr) && Guid.TryParse(userIdStr?.ToString(), out var userId))
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var member = await unitOfWork.Conversations.GetMemberAsync(conversationId, userId);
                if (member != null)
                {
                    member.LastReadAt = DateTimeOffset.UtcNow;
                    unitOfWork.Conversations.UpdateMember(member);
                    await unitOfWork.SaveChangesAsync();

                    // Broadcast read status
                    await Clients.Group($"conversation_{conversationId}").SendAsync("OnMessageSeen", new { ConversationId = conversationId, UserId = userId, SeenAt = member.LastReadAt });
                }
            }
        }
    }
}
