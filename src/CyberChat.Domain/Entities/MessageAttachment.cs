using System;
using CyberChat.Domain.Enums;

namespace CyberChat.Domain.Entities;

public class MessageAttachment
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public AttachmentType Type { get; set; }
    public string Url { get; set; } = null!;
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? DurationMs { get; set; }
    public string? StickerPackId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Message Message { get; set; } = null!;
}
