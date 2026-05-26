using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CyberChat.Domain.Entities;
using CyberChat.Application.Common.Models;

namespace CyberChat.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUidAsync(string uid, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUidAsync(string uid, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<PagedList<User>> SearchUsersAsync(string? query, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    void Update(User user);
}

public interface IUserSessionRepository
{
    Task<UserSession?> GetByRefreshTokenAsync(string token, CancellationToken cancellationToken = default);
    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);
    void Delete(UserSession session);
    void DeleteExpiredSessions(Guid userId);
}

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetDirectConversationAsync(Guid userAId, Guid userBId, CancellationToken cancellationToken = default);
    Task<PagedList<Conversation>> GetConversationsForUserAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ConversationMember?> GetMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<ConversationMember>> GetMembersAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task AddMemberAsync(ConversationMember member, CancellationToken cancellationToken = default);
    void RemoveMember(ConversationMember member);
    void Update(Conversation conversation);
    void UpdateMember(ConversationMember member);
}

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedList<Message>> GetMessagesAsync(Guid conversationId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(Message message, CancellationToken cancellationToken = default);
    void Update(Message message);
    Task<MessageReaction?> GetReactionAsync(Guid messageId, Guid userId, string emoji, CancellationToken cancellationToken = default);
    Task AddReactionAsync(MessageReaction reaction, CancellationToken cancellationToken = default);
    void RemoveReaction(MessageReaction reaction);
}

public interface IFriendRepository
{
    Task<FriendRequest?> GetRequestAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken = default);
    Task<FriendRequest?> GetRequestByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<FriendRequest>> GetPendingRequestsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddRequestAsync(FriendRequest request, CancellationToken cancellationToken = default);
    void RemoveRequest(FriendRequest request);

    Task<Friendship?> GetFriendshipAsync(Guid userAId, Guid userBId, CancellationToken cancellationToken = default);
    Task<List<User>> GetFriendsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddFriendshipAsync(Friendship friendship, CancellationToken cancellationToken = default);
    void RemoveFriendship(Friendship friendship);

    Task<Block?> GetBlockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default);
    Task<List<User>> GetBlockedUsersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddBlockAsync(Block block, CancellationToken cancellationToken = default);
    void RemoveBlock(Block block);
    Task<bool> IsBlockedAsync(Guid userAId, Guid userBId, CancellationToken cancellationToken = default);
}

public interface IStoryRepository
{
    Task<Story?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Story>> GetActiveStoriesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Story story, CancellationToken cancellationToken = default);
    void Delete(Story story);

    Task<StoryView?> GetViewAsync(Guid storyId, Guid viewerId, CancellationToken cancellationToken = default);
    Task AddViewAsync(StoryView view, CancellationToken cancellationToken = default);

    Task<StoryReaction?> GetReactionAsync(Guid storyId, Guid userId, CancellationToken cancellationToken = default);
    Task AddReactionAsync(StoryReaction reaction, CancellationToken cancellationToken = default);
    void RemoveReaction(StoryReaction reaction);

    Task<List<Story>> GetExpiredStoriesAsync(CancellationToken cancellationToken = default);
}

public interface ITodoRepository
{
    Task<TodoList?> GetListByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<TodoList>> GetListsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddListAsync(TodoList list, CancellationToken cancellationToken = default);
    void UpdateList(TodoList list);
    void DeleteList(TodoList list);

    Task<Todo?> GetTodoByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedList<Todo>> GetTodosAsync(Guid userId, Guid? listId, string? status, string? priority, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task AddTodoAsync(Todo todo, CancellationToken cancellationToken = default);
    void UpdateTodo(Todo todo);
    void DeleteTodo(Todo todo);

    Task<List<Todo>> GetPendingRemindersAsync(DateTimeOffset thresholdTime, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IUserSessionRepository UserSessions { get; }
    IConversationRepository Conversations { get; }
    IMessageRepository Messages { get; }
    IFriendRepository Friends { get; }
    IStoryRepository Stories { get; }
    ITodoRepository Todos { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
