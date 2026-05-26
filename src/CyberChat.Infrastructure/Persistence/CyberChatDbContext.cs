using Microsoft.EntityFrameworkCore;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Domain.Entities;
using CyberChat.Domain.Enums;

namespace CyberChat.Infrastructure.Persistence;

public class CyberChatDbContext : DbContext, ICyberChatDbContext
{
    public CyberChatDbContext(DbContextOptions<CyberChatDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<StoryView> StoryViews => Set<StoryView>();
    public DbSet<StoryReaction> StoryReactions => Set<StoryReaction>();
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<TodoTag> TodoTags => Set<TodoTag>();
    public DbSet<TodoAttachment> TodoAttachments => Set<TodoAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Register PostgreSQL enums
        modelBuilder.HasPostgresEnum<ConversationType>();
        modelBuilder.HasPostgresEnum<MemberRole>();
        modelBuilder.HasPostgresEnum<MessageContentType>();
        modelBuilder.HasPostgresEnum<AttachmentType>();
        modelBuilder.HasPostgresEnum<FriendshipStatus>();
        modelBuilder.HasPostgresEnum<FriendRequestStatus>();
        modelBuilder.HasPostgresEnum<StoryMediaType>();
        modelBuilder.HasPostgresEnum<TodoPriority>();
        modelBuilder.HasPostgresEnum<TodoStatus>();
        modelBuilder.HasPostgresEnum<DeviceType>();

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CyberChatDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
