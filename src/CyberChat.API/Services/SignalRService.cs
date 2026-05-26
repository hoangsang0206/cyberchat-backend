using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using CyberChat.Application.Common.Interfaces;
using CyberChat.API.Hubs;

namespace CyberChat.API.Services;

public class SignalRService : ISignalRService
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRService(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToUserAsync(Guid userId, string method, object arg, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"user_{userId}").SendAsync(method, arg, cancellationToken);
    }

    public async Task SendToUsersAsync(IEnumerable<Guid> userIds, string method, object arg, CancellationToken cancellationToken = default)
    {
        var tasks = userIds.Select(userId => 
            _hubContext.Clients.Group($"user_{userId}").SendAsync(method, arg, cancellationToken));
        await Task.WhenAll(tasks);
    }

    public async Task SendToConversationAsync(Guid conversationId, string method, object arg, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"conversation_{conversationId}").SendAsync(method, arg, cancellationToken);
    }
}
