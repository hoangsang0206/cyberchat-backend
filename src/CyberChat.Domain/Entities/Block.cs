using System;

namespace CyberChat.Domain.Entities;

public class Block
{
    public Guid Id { get; set; }
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual User Blocker { get; set; } = null!;
    public virtual User Blocked { get; set; } = null!;
}
