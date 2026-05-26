using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CyberChat.Application.Common.Interfaces;
using CyberChat.Application.Common.Models;
using CyberChat.Domain.Entities;
using CyberChat.Domain.Enums;

namespace CyberChat.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly CyberChatDbContext _context;

    public UserRepository(CyberChatDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByUidAsync(string uid, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Uid == uid, cancellationToken);
    }

    public async Task<bool> ExistsByUidAsync(string uid, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Uid == uid, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<PagedList<User>> SearchUsersAsync(string? query, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Users.AsNoTracking().Where(u => u.IsActive);

        if (!string.IsNullOrEmpty(query))
        {
            var lowerQuery = query.ToLower();
            dbQuery = dbQuery.Where(u => u.DisplayName.ToLower().Contains(lowerQuery) || u.Uid.ToLower().Contains(lowerQuery));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        
        var items = await dbQuery
            .OrderBy(u => u.DisplayName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<User>(items, totalCount, pageNumber, pageSize);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public void Update(User user)
    {
        _context.Users.Update(user);
    }
}

public class UserSessionRepository : IUserSessionRepository
{
    private readonly CyberChatDbContext _context;

    public UserSessionRepository(CyberChatDbContext context)
    {
        _context = context;
    }

    public async Task<UserSession?> GetByRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.UserSessions
            .FirstOrDefaultAsync(s => s.RefreshToken == token, cancellationToken);
    }

    public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        await _context.UserSessions.AddAsync(session, cancellationToken);
    }

    public void Delete(UserSession session)
    {
        _context.UserSessions.Remove(session);
    }

    public void DeleteExpiredSessions(Guid userId)
    {
        var expired = _context.UserSessions.Where(s => s.UserId == userId && s.ExpiresAt < DateTimeOffset.UtcNow);
        _context.UserSessions.RemoveRange(expired);
    }
}

public class ConversationRepository : IConversationRepository
{
    private readonly CyberChatDbContext _context;

    public ConversationRepository(CyberChatDbContext context)
    {
        _context = context;
    }

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .Include(c => c.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Conversation?> GetDirectConversationAsync(Guid userAId, Guid userBId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .Include(c => c.Members)
                .ThenInclude(m => m.User)
            .Where(c => c.Type == ConversationType.Direct)
            .FirstOrDefaultAsync(c => c.Members.Any(m => m.UserId == userAId) && c.Members.Any(m => m.UserId == userBId), cancellationToken);
    }

    public async Task<PagedList<Conversation>> GetConversationsForUserAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Conversations
            .AsNoTracking()
            .Include(c => c.Members)
                .ThenInclude(m => m.User)
            .Include(c => c.Messages)
            .Where(c => c.Members.Any(m => m.UserId == userId));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<Conversation>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<ConversationMember?> GetMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId, cancellationToken);
    }

    public async Task<List<ConversationMember>> GetMembersAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.ConversationId == conversationId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await _context.Conversations.AddAsync(conversation, cancellationToken);
    }

    public async Task AddMemberAsync(ConversationMember member, CancellationToken cancellationToken = default)
    {
        await _context.ConversationMembers.AddAsync(member, cancellationToken);
    }

    public void RemoveMember(ConversationMember member)
    {
        _context.ConversationMembers.Remove(member);
    }

    public void Update(Conversation conversation)
    {
        _context.Conversations.Update(conversation);
    }

    public void UpdateMember(ConversationMember member)
    {
        _context.ConversationMembers.Update(member);
    }
}

public class MessageRepository : IMessageRepository
{
    private readonly CyberChatDbContext _context;

    public MessageRepository(CyberChatDbContext context)
    {
        _context = context;
    }

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
                .ThenInclude(r => r.User)
            .Include(m => m.ReplyTo)
                .ThenInclude(r => r!.Sender)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<PagedList<Message>> GetMessagesAsync(Guid conversationId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
                .ThenInclude(r => r.User)
            .Include(m => m.ReplyTo)
                .ThenInclude(r => r!.Sender)
            .Where(m => m.ConversationId == conversationId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<Message>(items, totalCount, pageNumber, pageSize);
    }

    public async Task AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        await _context.Messages.AddAsync(message, cancellationToken);
    }

    public void Update(Message message)
    {
        _context.Messages.Update(message);
    }

    public async Task<MessageReaction?> GetReactionAsync(Guid messageId, Guid userId, string emoji, CancellationToken cancellationToken = default)
    {
        return await _context.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji, cancellationToken);
    }

    public async Task AddReactionAsync(MessageReaction reaction, CancellationToken cancellationToken = default)
    {
        await _context.MessageReactions.AddAsync(reaction, cancellationToken);
    }

    public void RemoveReaction(MessageReaction reaction)
    {
        _context.MessageReactions.Remove(reaction);
    }
}

public class FriendRepository : IFriendRepository
{
    private readonly CyberChatDbContext _context;

    public FriendRepository(CyberChatDbContext context)
    {
        _context = context;
    }

    public async Task<FriendRequest?> GetRequestAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken = default)
    {
        return await _context.FriendRequests
            .FirstOrDefaultAsync(r => r.FromUserId == fromUserId && r.ToUserId == toUserId, cancellationToken);
    }

    public async Task<FriendRequest?> GetRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FriendRequests
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<FriendRequest>> GetPendingRequestsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.FriendRequests
            .AsNoTracking()
            .Include(r => r.FromUser)
            .Include(r => r.ToUser)
            .Where(r => r.ToUserId == userId && r.Status == FriendRequestStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRequestAsync(FriendRequest request, CancellationToken cancellationToken = default)
    {
        await _context.FriendRequests.AddAsync(request, cancellationToken);
    }

    public void RemoveRequest(FriendRequest request)
    {
        _context.FriendRequests.Remove(request);
    }

    public async Task<Friendship?> GetFriendshipAsync(Guid userAId, Guid userBId, CancellationToken cancellationToken = default)
    {
        var id1 = userAId < userBId ? userAId : userBId;
        var id2 = userAId < userBId ? userBId : userAId;

        return await _context.Friendships
            .FirstOrDefaultAsync(f => f.UserAId == id1 && f.UserBId == id2, cancellationToken);
    }

    public async Task<List<User>> GetFriendsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // friendships table links user_a_id < user_b_id. So friends are user_b if we are user_a, or user_a if we are user_b
        var friendsA = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.UserAId == userId)
            .Select(f => f.UserB)
            .ToListAsync(cancellationToken);

        var friendsB = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.UserBId == userId)
            .Select(f => f.UserA)
            .ToListAsync(cancellationToken);

        return friendsA.Concat(friendsB).ToList();
    }

    public async Task AddFriendshipAsync(Friendship friendship, CancellationToken cancellationToken = default)
    {
        await _context.Friendships.AddAsync(friendship, cancellationToken);
    }

    public void RemoveFriendship(Friendship friendship)
    {
        _context.Friendships.Remove(friendship);
    }

    public async Task<Block?> GetBlockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
    {
        return await _context.Blocks
            .FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, cancellationToken);
    }

    public async Task<List<User>> GetBlockedUsersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Blocks
            .AsNoTracking()
            .Where(b => b.BlockerId == userId)
            .Select(b => b.Blocked)
            .ToListAsync(cancellationToken);
    }

    public async Task AddBlockAsync(Block block, CancellationToken cancellationToken = default)
    {
        await _context.Blocks.AddAsync(block, cancellationToken);
    }

    public void RemoveBlock(Block block)
    {
        _context.Blocks.Remove(block);
    }

    public async Task<bool> IsBlockedAsync(Guid userAId, Guid userBId, CancellationToken cancellationToken = default)
    {
        // Checks if userA blocked userB
        return await _context.Blocks
            .AnyAsync(b => b.BlockerId == userAId && b.BlockedId == userBId, cancellationToken);
    }
}

public class StoryRepository : IStoryRepository
{
    private readonly CyberChatDbContext _context;

    public StoryRepository(CyberChatDbContext context)
    {
        _context = context;
    }

    public async Task<Story?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Stories
            .Include(s => s.User)
            .Include(s => s.Views)
                .ThenInclude(v => v.Viewer)
            .Include(s => s.Reactions)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<List<Story>> GetActiveStoriesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Get friends' user IDs
        var friendsA = _context.Friendships.Where(f => f.UserAId == userId).Select(f => f.UserBId);
        var friendsB = _context.Friendships.Where(f => f.UserBId == userId).Select(f => f.UserAId);
        var directFriendIds = await friendsA.Concat(friendsB).ToListAsync(cancellationToken);
        
        // Include the user themselves in the feed
        directFriendIds.Add(userId);

        return await _context.Stories
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Views)
                .ThenInclude(v => v.Viewer)
            .Include(s => s.Reactions)
            .Where(s => directFriendIds.Contains(s.UserId) && s.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Story story, CancellationToken cancellationToken = default)
    {
        await _context.Stories.AddAsync(story, cancellationToken);
    }

    public void Delete(Story story)
    {
        _context.Stories.Remove(story);
    }

    public async Task<StoryView?> GetViewAsync(Guid storyId, Guid viewerId, CancellationToken cancellationToken = default)
    {
        return await _context.StoryViews
            .FirstOrDefaultAsync(v => v.StoryId == storyId && v.ViewerId == viewerId, cancellationToken);
    }

    public async Task AddViewAsync(StoryView view, CancellationToken cancellationToken = default)
    {
        await _context.StoryViews.AddAsync(view, cancellationToken);
    }

    public async Task<StoryReaction?> GetReactionAsync(Guid storyId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.StoryReactions
            .FirstOrDefaultAsync(r => r.StoryId == storyId && r.UserId == userId, cancellationToken);
    }

    public async Task AddReactionAsync(StoryReaction reaction, CancellationToken cancellationToken = default)
    {
        await _context.StoryReactions.AddAsync(reaction, cancellationToken);
    }

    public void RemoveReaction(StoryReaction reaction)
    {
        _context.StoryReactions.Remove(reaction);
    }

    public async Task<List<Story>> GetExpiredStoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Stories
            .Where(s => s.ExpiresAt <= DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);
    }
}

public class TodoRepository : ITodoRepository
{
    private readonly CyberChatDbContext _context;

    public TodoRepository(CyberChatDbContext context)
    {
        _context = context;
    }

    public async Task<TodoList?> GetListByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TodoLists
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<List<TodoList>> GetListsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.TodoLists
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task AddListAsync(TodoList list, CancellationToken cancellationToken = default)
    {
        await _context.TodoLists.AddAsync(list, cancellationToken);
    }

    public void UpdateList(TodoList list)
    {
        _context.TodoLists.Update(list);
    }

    public void DeleteList(TodoList list)
    {
        _context.TodoLists.Remove(list);
    }

    public async Task<Todo?> GetTodoByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Todos
            .Include(t => t.Tags)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<PagedList<Todo>> GetTodosAsync(Guid userId, Guid? listId, string? status, string? priority, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Todos
            .AsNoTracking()
            .Include(t => t.Tags)
            .Include(t => t.Attachments)
            .Where(t => t.UserId == userId);

        if (listId.HasValue)
        {
            query = query.Where(t => t.ListId == listId.Value);
        }

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TodoStatus>(status, true, out var todoStatus))
        {
            query = query.Where(t => t.Status == todoStatus);
        }

        if (!string.IsNullOrEmpty(priority) && Enum.TryParse<TodoPriority>(priority, true, out var todoPriority))
        {
            query = query.Where(t => t.Priority == todoPriority);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<Todo>(items, totalCount, pageNumber, pageSize);
    }

    public async Task AddTodoAsync(Todo todo, CancellationToken cancellationToken = default)
    {
        await _context.Todos.AddAsync(todo, cancellationToken);
    }

    public void UpdateTodo(Todo todo)
    {
        _context.Todos.Update(todo);
    }

    public void DeleteTodo(Todo todo)
    {
        _context.Todos.Remove(todo);
    }

    public async Task<List<Todo>> GetPendingRemindersAsync(DateTimeOffset thresholdTime, CancellationToken cancellationToken = default)
    {
        // Find todos that are not completed, have a due date in the near future, and reminder configuration active
        return await _context.Todos
            .Include(t => t.User)
            .Where(t => t.Status != TodoStatus.Done && t.Status != TodoStatus.Cancelled 
                && t.DueDate.HasValue && t.ReminderMinutes.HasValue 
                && t.DueDate.Value.AddMinutes(-t.ReminderMinutes.Value) <= thresholdTime)
            .ToListAsync(cancellationToken);
    }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly CyberChatDbContext _context;

    public IUserRepository Users { get; }
    public IUserSessionRepository UserSessions { get; }
    public IConversationRepository Conversations { get; }
    public IMessageRepository Messages { get; }
    public IFriendRepository Friends { get; }
    public IStoryRepository Stories { get; }
    public ITodoRepository Todos { get; }

    public UnitOfWork(
        CyberChatDbContext context,
        IUserRepository users,
        IUserSessionRepository userSessions,
        IConversationRepository conversations,
        IMessageRepository messages,
        IFriendRepository friends,
        IStoryRepository stories,
        ITodoRepository todos)
    {
        _context = context;
        Users = users;
        UserSessions = userSessions;
        Conversations = conversations;
        Messages = messages;
        Friends = friends;
        Stories = stories;
        Todos = todos;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
