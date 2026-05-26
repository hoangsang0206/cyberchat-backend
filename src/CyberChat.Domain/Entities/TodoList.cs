using System;
using System.Collections.Generic;

namespace CyberChat.Domain.Entities;

public class TodoList
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public string Color { get; set; } = "#6366F1";
    public string? Icon { get; set; }
    public bool IsDefault { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ICollection<Todo> Todos { get; set; } = new List<Todo>();
}
