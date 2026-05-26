using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CyberChat.Domain.Entities;

namespace CyberChat.Application.Common.Interfaces;

public interface ICyberChatDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationMember> ConversationMembers { get; }
    DbSet<Message> Messages { get; }
    DbSet<MessageAttachment> MessageAttachments { get; }
    DbSet<MessageReaction> MessageReactions { get; }
    DbSet<FriendRequest> FriendRequests { get; }
    DbSet<Friendship> Friendships { get; }
    DbSet<Block> Blocks { get; }
    DbSet<Story> Stories { get; }
    DbSet<StoryView> StoryViews { get; }
    DbSet<StoryReaction> StoryReactions { get; }
    DbSet<TodoList> TodoLists { get; }
    DbSet<Todo> Todos { get; }
    DbSet<TodoTag> TodoTags { get; }
    DbSet<TodoAttachment> TodoAttachments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
