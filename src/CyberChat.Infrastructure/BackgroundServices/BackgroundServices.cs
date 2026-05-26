using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CyberChat.Application.Common.Interfaces;

namespace CyberChat.Infrastructure.BackgroundServices;

public class StoryCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StoryCleanupBackgroundService> _logger;

    public StoryCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<StoryCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Story Cleanup Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var expiredStories = await unitOfWork.Stories.GetExpiredStoriesAsync(stoppingToken);

                    if (expiredStories.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} expired stories. Commencing deletion.", expiredStories.Count);
                        foreach (var story in expiredStories)
                        {
                            unitOfWork.Stories.Delete(story);
                        }
                        await unitOfWork.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while cleaning up expired stories.");
            }

            // Wait 1 hour before running again
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        _logger.LogInformation("Story Cleanup Background Service is stopping.");
    }
}

public class TodoReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TodoReminderBackgroundService> _logger;

    public TodoReminderBackgroundService(IServiceProvider serviceProvider, ILogger<TodoReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Todo Reminder Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var signalRService = scope.ServiceProvider.GetRequiredService<ISignalRService>();
                    var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

                    // Check reminders threshold (items whose scheduled reminder time is in the past)
                    var threshold = dateTimeProvider.UtcNow;
                    var pendingReminders = await unitOfWork.Todos.GetPendingRemindersAsync(threshold, stoppingToken);

                    if (pendingReminders.Count > 0)
                    {
                        _logger.LogInformation("Processing {Count} pending todo reminders.", pendingReminders.Count);
                        
                        foreach (var todo in pendingReminders)
                        {
                            // Trigger SignalR realtime notification
                            await signalRService.SendToUserAsync(
                                todo.UserId, 
                                "OnTodoReminder", 
                                new { TodoId = todo.Id, Title = todo.Title, DueDate = todo.DueDate }, 
                                stoppingToken);

                            // Disable future reminders by setting reminder minutes to null (so they don't fire repeatedly)
                            todo.ReminderMinutes = null;
                            unitOfWork.Todos.UpdateTodo(todo);
                        }

                        await unitOfWork.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during Todo reminder check.");
            }

            // Run check every 1 minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("Todo Reminder Background Service is stopping.");
    }
}
