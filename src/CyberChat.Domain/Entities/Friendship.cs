using System;

namespace CyberChat.Domain.Entities;

public class Friendship
{
    public Guid Id { get; set; }
    public Guid UserAId { get; set; }
    public Guid UserBId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual User UserA { get; set; } = null!;
    public virtual User UserB { get; set; } = null!;
}
