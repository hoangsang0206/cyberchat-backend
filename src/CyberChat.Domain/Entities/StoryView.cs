using System;

namespace CyberChat.Domain.Entities;

public class StoryView
{
    public Guid Id { get; set; }
    public Guid StoryId { get; set; }
    public Guid ViewerId { get; set; }
    public DateTimeOffset ViewedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Story Story { get; set; } = null!;
    public virtual User Viewer { get; set; } = null!;
}
