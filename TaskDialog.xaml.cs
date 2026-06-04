using System.Windows;
using System.Windows.Controls;
using MokReport.Todo.Models;

namespace MokReport.Todo;

public partial class TaskDialog : Window
{
    public TodoItem Result { get; private set; } = new();

    public TaskDialog()
    {
        InitializeComponent();
        DueDatePicker.SelectedDate = DateTime.Today;
    }

    public TaskDialog(TodoItem existing) : this()
    {
        DialogTitle.Text = "✏️ 编辑任务";
        Result = existing;
        TitleBox.Text = existing.Title;
        DescBox.Text = existing.Description ?? "";
        DueDatePicker.SelectedDate = existing.DueDate;
        PriorityBox.SelectedIndex = Math.Clamp(existing.Priority - 1, 0, 2);
        CategoryBox.SelectedIndex = Math.Max(0, Array.IndexOf(TodoItem.Categories, existing.Category));

        if (existing.ReminderTime.HasValue)
        {
            ReminderHour.SelectedIndex = existing.ReminderTime.Value.Hour;
            var min = existing.ReminderTime.Value.Minute;
            ReminderMin.SelectedIndex = min switch { 15 => 1, 30 => 2, 45 => 3, _ => 0 };
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show("至少输入一个任务名称哦～ (◕︵◕)", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var categoryIndex = CategoryBox.SelectedIndex;
        var category = categoryIndex >= 0 && categoryIndex < TodoItem.Categories.Length
            ? TodoItem.Categories[categoryIndex] : "📋 一般";

        Result = Result with
        {
            Title = TitleBox.Text.Trim(),
            Description = string.IsNullOrWhiteSpace(DescBox.Text) ? null : DescBox.Text.Trim(),
            Category = category,
            Priority = PriorityBox.SelectedIndex + 1,
            DueDate = DueDatePicker.SelectedDate?.Date,
            ReminderTime = BuildReminderTime()
        };

        DialogResult = true;
        Close();
    }

    private DateTime? BuildReminderTime()
    {
        try
        {
            if (ReminderHour.SelectedItem is not ComboBoxItem hourItem || ReminderMin.SelectedItem is not ComboBoxItem minItem)
                return null;

            var hourStr = hourItem.Content?.ToString()?.Replace("时", "") ?? "";
            var minStr = minItem.Content?.ToString()?.Replace("分", "") ?? "";

            if (!int.TryParse(hourStr, out var hour) || !int.TryParse(minStr, out var min))
                return null;

            return DateTime.Today.AddHours(hour).AddMinutes(min);
        }
        catch { return null; }
    }

    private void QuickReminder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag == null) return;
        var tag = btn.Tag.ToString()!;

        DateTime reminderTime;
        if (tag == "tomorrow9")
            reminderTime = DateTime.Today.AddDays(1).AddHours(9);
        else if (int.TryParse(tag, out var minutes))
            reminderTime = DateTime.Now.AddMinutes(minutes);
        else return;

        ReminderHour.SelectedIndex = reminderTime.Hour;
        var min = reminderTime.Minute;
        ReminderMin.SelectedIndex = min switch { >= 45 => 3, >= 30 => 2, >= 15 => 1, _ => 0 };
    }

    private void ClearDue_Click(object sender, RoutedEventArgs e) => DueDatePicker.SelectedDate = null;
    private void ClearReminder_Click(object sender, RoutedEventArgs e)
    {
        ReminderHour.SelectedIndex = -1;
        ReminderMin.SelectedIndex = -1;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
