using System;
using System.Collections.Generic;
using CyberChat.Domain.Enums;

namespace CyberChat.Domain.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    public ConversationType Type { get; set; } = ConversationType.Direct;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual User Creator { get; set; } = null!;
    public virtual ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
