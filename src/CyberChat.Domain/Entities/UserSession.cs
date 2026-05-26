using System;
using System.Net;
using CyberChat.Domain.Enums;

namespace CyberChat.Domain.Entities;

public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? DeviceName { get; set; }
    public DeviceType DeviceType { get; set; } = DeviceType.Web;
    public IPAddress? IpAddress { get; set; }
    public string? FcmToken { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual User User { get; set; } = null!;
}
