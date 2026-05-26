using System;
using System.Collections.Generic;

namespace CyberChat.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Uid { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public virtual ICollection<Conversation> ConversationsCreated { get; set; } = new List<Conversation>();
    public virtual ICollection<ConversationMember> Memberships { get; set; } = new List<ConversationMember>();
    public virtual ICollection<Message> MessagesSent { get; set; } = new List<Message>();
    public virtual ICollection<MessageReaction> MessageReactions { get; set; } = new List<MessageReaction>();
    public virtual ICollection<FriendRequest> FriendRequestsSent { get; set; } = new List<FriendRequest>();
    public virtual ICollection<FriendRequest> FriendRequestsReceived { get; set; } = new List<FriendRequest>();
    public virtual ICollection<Friendship> FriendshipsA { get; set; } = new List<Friendship>();
    public virtual ICollection<Friendship> FriendshipsB { get; set; } = new List<Friendship>();
    public virtual ICollection<Block> BlocksInitiated { get; set; } = new List<Block>();
    public virtual ICollection<Block> BlocksReceived { get; set; } = new List<Block>();
    public virtual ICollection<Story> Stories { get; set; } = new List<Story>();
    public virtual ICollection<StoryView> StoryViews { get; set; } = new List<StoryView>();
    public virtual ICollection<StoryReaction> StoryReactions { get; set; } = new List<StoryReaction>();
    public virtual ICollection<TodoList> TodoLists { get; set; } = new List<TodoList>();
    public virtual ICollection<Todo> Todos { get; set; } = new List<Todo>();
}
