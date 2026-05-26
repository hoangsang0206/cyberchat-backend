using System;

namespace CyberChat.Domain.Entities;

public class TodoTag
{
    public Guid Id { get; set; }
    public Guid TodoId { get; set; }
    public string Tag { get; set; } = null!;

    // Navigation properties
    public virtual Todo Todo { get; set; } = null!;
}
