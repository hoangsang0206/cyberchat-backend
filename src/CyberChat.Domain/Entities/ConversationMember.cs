using System;
using CyberChat.Domain.Enums;

namespace CyberChat.Domain.Entities;

public class ConversationMember
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public MemberRole Role { get; set; } = MemberRole.Member;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastReadAt { get; set; }
    public bool IsMuted { get; set; } = false;

    // Navigation properties
    public virtual Conversation Conversation { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
