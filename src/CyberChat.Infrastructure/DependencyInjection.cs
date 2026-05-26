using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Domain.Enums;
using CyberChat.Infrastructure.BackgroundServices;
using CyberChat.Infrastructure.Persistence;
using CyberChat.Infrastructure.Persistence.Repositories;
using CyberChat.Infrastructure.Services;

namespace CyberChat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. PostgreSQL DbContext Setup with custom enums mapping
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Database=cyberchat;Username=postgres;Password=postgres";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<ConversationType>();
        dataSourceBuilder.MapEnum<MemberRole>();
        dataSourceBuilder.MapEnum<MessageContentType>();
        dataSourceBuilder.MapEnum<AttachmentType>();
        dataSourceBuilder.MapEnum<FriendshipStatus>();
        dataSourceBuilder.MapEnum<FriendRequestStatus>();
        dataSourceBuilder.MapEnum<StoryMediaType>();
        dataSourceBuilder.MapEnum<TodoPriority>();
        dataSourceBuilder.MapEnum<TodoStatus>();
        dataSourceBuilder.MapEnum<DeviceType>();

        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<CyberChatDbContext>(options =>
            options.UseNpgsql(dataSource));

        services.AddScoped<ICyberChatDbContext>(provider => provider.GetRequiredService<CyberChatDbContext>());

        // 2. Redis ConnectionMultiplexer Setup
        var redisConn = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConn));

        // 3. Repositories Registration
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IFriendRepository, FriendRepository>();
        services.AddScoped<IStoryRepository, StoryRepository>();
        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // 4. Infrastructure Services Registration
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<IStorageService, StorageService>();

        // 5. Background Hosted Services
        services.AddHostedService<StoryCleanupBackgroundService>();
        services.AddHostedService<TodoReminderBackgroundService>();

        return services;
    }
}
