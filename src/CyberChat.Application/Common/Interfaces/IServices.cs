using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CyberChat.Domain.Entities;

namespace CyberChat.Application.Common.Interfaces;

public interface IJwtProvider
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Guid? GetUserIdFromToken(string token);
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}

public interface IStorageService
{
    Task<string> UploadFileAsync(string bucketName, string fileName, Stream stream, string contentType, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string bucketName, string fileName, CancellationToken cancellationToken = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    // Online presence tracking
    Task SetUserOnlineAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);
    Task SetUserOfflineAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetOnlineUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> IsUserOnlineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<string>> GetUserConnectionsAsync(Guid userId, CancellationToken cancellationToken = default);

    // Typing state tracking
    Task SetUserTypingAsync(Guid conversationId, Guid userId, bool isTyping, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetTypingUsersAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

public interface ISignalRService
{
    Task SendToUserAsync(Guid userId, string method, object arg, CancellationToken cancellationToken = default);
    Task SendToUsersAsync(IEnumerable<Guid> userIds, string method, object arg, CancellationToken cancellationToken = default);
    Task SendToConversationAsync(Guid conversationId, string method, object arg, CancellationToken cancellationToken = default);
}

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
