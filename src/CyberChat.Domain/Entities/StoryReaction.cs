using System;

namespace CyberChat.Domain.Entities;

public class StoryReaction
{
    public Guid Id { get; set; }
    public Guid StoryId { get; set; }
    public Guid UserId { get; set; }
    public string Emoji { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Story Story { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
