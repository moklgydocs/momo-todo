namespace MokReport.Todo.Models;

public record TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? DueDate { get; set; }
    public DateTime? ReminderTime { get; set; }
    public bool ReminderShown { get; set; }
    public string Category { get; set; } = "📋 一般";
    public int Priority { get; set; } = 1;
    public string Color { get; set; } = "";
    public string Repeat { get; set; } = "None"; // None/Daily/Weekly/Monthly
    public string Defer { get; set; } = "Default"; // Default/NoDefer/AutoDefer

    public bool IsOverdue => !IsCompleted && DueDate.HasValue && DueDate.Value < DateTime.Now;
    public bool IsDueSoon => !IsCompleted && DueDate.HasValue &&
        DueDate.Value > DateTime.Now && DueDate.Value < DateTime.Now.AddHours(24);
    public bool NeedsReminder => !IsCompleted && ReminderTime.HasValue &&
        ReminderTime.Value <= DateTime.Now && !ReminderShown;

    public static readonly string[] Categories = { "📋 一般", "💼 工作", "🏠 个人", "🛒 购物", "❤️ 健康", "📚 学习", "🎮 娱乐" };
    public static readonly string[] Colors = { "", "#FF8B9F", "#A78BFA", "#6EE7B7", "#FCD34D", "#38BDF8", "#FB923C", "#EF4444" };
    public static readonly string[] ColorNames = { "无", "粉", "紫", "绿", "黄", "蓝", "橙", "红" };
    public static readonly string[] Repeats = { "None", "Daily", "Weekly", "Monthly" };
    public static readonly string[] RepeatNames = { "不重复", "每天", "每周", "每月" };
    public static readonly string[] Defers = { "Default", "NoDefer", "AutoDefer" };
    public static readonly string[] DeferNames = { "默认", "不延期", "自动延期" };
}
