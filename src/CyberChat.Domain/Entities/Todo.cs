using System;
using System.Collections.Generic;
using CyberChat.Domain.Enums;

namespace CyberChat.Domain.Entities;

public class Todo
{
    public Guid Id { get; set; }
    public Guid ListId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public bool IsAllDay { get; set; } = false;
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public TodoStatus Status { get; set; } = TodoStatus.Todo;
    public int? ReminderMinutes { get; set; }
    public string? RecurrenceRule { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual TodoList List { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<TodoTag> Tags { get; set; } = new List<TodoTag>();
    public virtual ICollection<TodoAttachment> Attachments { get; set; } = new List<TodoAttachment>();
}
