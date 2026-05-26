using System;
using System.Collections.Generic;
using CyberChat.Domain.Enums;

namespace CyberChat.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public MessageContentType ContentType { get; set; } = MessageContentType.Text;
    public string? TextContent { get; set; }
    public Guid? ReplyToId { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Conversation Conversation { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
    public virtual Message? ReplyTo { get; set; }
    public virtual ICollection<Message> Replies { get; set; } = new List<Message>();
    public virtual ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
    public virtual ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
}
