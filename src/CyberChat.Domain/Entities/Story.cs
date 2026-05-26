using System;
using System.Collections.Generic;
using CyberChat.Domain.Enums;

namespace CyberChat.Domain.Entities;

public class Story
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string MediaUrl { get; set; } = null!;
    public StoryMediaType MediaType { get; set; }
    public string? Caption { get; set; }
    public string? BgColor { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ICollection<StoryView> Views { get; set; } = new List<StoryView>();
    public virtual ICollection<StoryReaction> Reactions { get; set; } = new List<StoryReaction>();
}
