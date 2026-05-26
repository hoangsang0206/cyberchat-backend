using System;

namespace CyberChat.Domain.Entities;

public class MessageReaction
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public string Emoji { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Message Message { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
