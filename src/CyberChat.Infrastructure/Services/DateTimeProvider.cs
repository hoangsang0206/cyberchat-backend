using System;
using CyberChat.Application.Common.Interfaces;

namespace CyberChat.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
