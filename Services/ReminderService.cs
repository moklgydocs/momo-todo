using System.Timers;
using Microsoft.Toolkit.Uwp.Notifications;
using MokReport.Todo.Models;
using Timer = System.Timers.Timer;

namespace MokReport.Todo.Services;

public class ReminderService : IDisposable
{
    private readonly TodoService _todoService;
    private readonly Timer _timer;

    public ReminderService(TodoService todoService)
    {
        _todoService = todoService;
        _timer = new Timer(30_000); // Check every 30 seconds
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var reminders = _todoService.GetPendingReminders();
            foreach (var item in reminders)
            {
                ShowReminder(item);
                _todoService.MarkReminderShown(item.Id);
            }
        }
        catch { /* Silently handle timer errors */ }
    }

    private static void ShowReminder(TodoItem item)
    {
        var priorityEmoji = item.Priority switch
        {
            3 => "🔴",
            2 => "🟡",
            _ => "⚪"
        };

        new ToastContentBuilder()
            .AddArgument("action", "viewTodo")
            .AddArgument("todoId", item.Id.ToString())
            .AddText($"⏰ {item.Title}", hintMaxLines: 1)
            .AddText(item.Description ?? $"到了哦～ 记得完成这个任务呢 (｡>﹏<｡)")
            .AddText($"{priorityEmoji} {item.Category}  ·  {item.DueDate:MM-dd HH:mm}")
            .AddAppLogoOverride(new Uri("https://img.icons8.com/emoji/96/pencil-emoji.png"),
                ToastGenericAppLogoCrop.Circle)
            .AddAudio(new Uri("ms-winsoundevent:Notification.Reminder"), false)
            .Show();
    }

    public void Dispose() => _timer?.Dispose();
}
