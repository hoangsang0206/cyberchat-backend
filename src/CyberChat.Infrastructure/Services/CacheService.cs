using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using CyberChat.Application.Common.Interfaces;

namespace CyberChat.Infrastructure.Services;

public class CacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public CacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = _redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        if (expiration.HasValue)
        {
            await _db.StringSetAsync(key, json, expiration.Value);
        }
        else
        {
            await _db.StringSetAsync(key, json);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _db.KeyDeleteAsync(key);
    }

    // Online Presence
    public async Task SetUserOnlineAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        var connKey = $"presence:connections:{userId}";
        await _db.SetAddAsync(connKey, connectionId);
        
        // Add to global online users set
        await _db.SetAddAsync("presence:online_users", userId.ToString());
    }

    public async Task SetUserOfflineAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        var connKey = $"presence:connections:{userId}";
        await _db.SetRemoveAsync(connKey, connectionId);

        // If no more connections exist for this user, they are truly offline
        var count = await _db.SetLengthAsync(connKey);
        if (count == 0)
        {
            await _db.SetRemoveAsync("presence:online_users", userId.ToString());
            await _db.KeyDeleteAsync(connKey);
        }
    }

    public async Task<List<Guid>> GetOnlineUsersAsync(CancellationToken cancellationToken = default)
    {
        var members = await _db.SetMembersAsync("presence:online_users");
        return members.Select(m => Guid.Parse(m!)).ToList();
    }

    public async Task<bool> IsUserOnlineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.SetContainsAsync("presence:online_users", userId.ToString());
    }

    public async Task<List<string>> GetUserConnectionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var members = await _db.SetMembersAsync($"presence:connections:{userId}");
        return members.Select(m => m.ToString()).ToList();
    }

    // Typing Status
    public async Task SetUserTypingAsync(Guid conversationId, Guid userId, bool isTyping, CancellationToken cancellationToken = default)
    {
        var key = $"typing:{conversationId}";
        if (isTyping)
        {
            await _db.SetAddAsync(key, userId.ToString());
            // Expire typing state after 5 seconds automatically to avoid hanging state
            await _db.KeyExpireAsync(key, TimeSpan.FromSeconds(5));
        }
        else
        {
            await _db.SetRemoveAsync(key, userId.ToString());
        }
    }

    public async Task<List<Guid>> GetTypingUsersAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var members = await _db.SetMembersAsync($"typing:{conversationId}");
        return members.Select(m => Guid.Parse(m!)).ToList();
    }
}
