using System;

namespace CyberChat.Domain.Entities;

public class TodoAttachment
{
    public Guid Id { get; set; }
    public Guid TodoId { get; set; }
    public string Url { get; set; } = null!;
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Todo Todo { get; set; } = null!;
}
