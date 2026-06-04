using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MokReport.Todo.Models;
using MokReport.Todo.Services;
using WF = System.Windows.Forms;
using System.Windows.Threading;

namespace MokReport.Todo;

public partial class MainWindow : Window
{
    private readonly TodoService _todoService;
    private readonly ReminderService _reminderService;
    private readonly WF.NotifyIcon _trayIcon;
    private List<TodoItem> _allItems = [];
    private string _currentFilter = "active";
    private int? _editingId;
    private int? _pendingDeleteId;
    private string _searchText = "";
    private string _activeCategory = "";
    private bool _isClosing;
    private bool _saving;
    private string _selectedColor = "";
    private Border[] _colorBtns = [];
    private DateTime _selectedDate = DateTime.Today;
    private DateTime _navMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private bool _showCalendar;
    private Button[] _weekBtns = Array.Empty<Button>();
    private DispatcherTimer? _toastTimer;
    private DispatcherTimer? _focusTimer;
    private int _focusMinutes;

    // Chinese holidays for 2026 (simplified - lunar dates approximated)
    private static readonly Dictionary<string, string> Holidays = new()
    {
        ["2026-01-01"] = "元旦",
        ["2026-02-17"] = "除夕",
        ["2026-02-18"] = "春节",
        ["2026-02-19"] = "初二",
        ["2026-02-20"] = "初三",
        ["2026-04-05"] = "清明",
        ["2026-05-01"] = "劳动节",
        ["2026-06-01"] = "儿童节",
        ["2026-06-05"] = "芒种",
        ["2026-06-21"] = "夏至",
        ["2026-06-21"] = "端午",
        ["2026-09-25"] = "中秋",
        ["2026-10-01"] = "国庆节",
        ["2026-12-22"] = "冬至",
        ["2026-12-25"] = "圣诞节",
    };

    public MainWindow()
    {
        InitializeComponent();
        _todoService = new TodoService();
        _reminderService = new ReminderService(_todoService);

        // System tray
        var menu = new WF.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (s, e) => ShowFromTray());
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("退出", null, (s, e) => { _isClosing = true; Close(); });
        _trayIcon = new WF.NotifyIcon
        {
            Text = "Momo Todo",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (s, e) => ShowFromTray();
        _trayIcon.Icon = System.Drawing.SystemIcons.Application;

        EditDuePicker.SelectedDate = DateTime.Today;
        InitWeekBar();
        InitCategoryPills();
        InitColorPicker();
        CheckStartupStatus();
        UpdateMonthLabel();
        LoadTasks();
        RefreshQuote();
    }

    #region Window Chrome

    private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) { _isClosing = true; Close(); }
    private void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized) Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isClosing) { e.Cancel = true; Hide(); return; }
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnClosing(e);
    }

    #endregion

    #region Week Bar

    private void InitWeekBar()
    {
        for (int i = 0; i < 7; i++)
            WeekBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _weekBtns = new Button[7];
        for (int i = 0; i < 7; i++)
        {
            var btn = new Button
            {
                Height = 56, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            btn.Click += WeekDay_Click;
            _weekBtns[i] = btn;
            Grid.SetColumn(btn, i);
            WeekBar.Children.Add(btn);
        }
        UpdateWeekBar();
    }

    private void UpdateWeekBar()
    {
        // Find Monday of the current week
        var diff = (7 + (int)_selectedDate.DayOfWeek - 1) % 7;
        var monday = _selectedDate.AddDays(-diff);

        for (int i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);
            var dayName = new[] { "一", "二", "三", "四", "五", "六", "日" }[i];
            var btn = _weekBtns[i];
            btn.Content = dayName;
            btn.Tag = date;

            var isToday = date.Date == DateTime.Today;
            var isSelected = date.Date == _selectedDate.Date;
            var isWeekend = i >= 5;

            // Update template via background
            if (isSelected)
            {
                btn.Background = FindResource("PrimaryBrush") as Brush;
                btn.Foreground = Brushes.White;
            }
            else
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = FindResource("TextPrimaryBrush") as Brush;
            }

            // Rebuild content template
            var bd = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = isSelected
                    ? FindResource("PrimaryBrush") as Brush
                    : Brushes.Transparent,
                Padding = new Thickness(4, 6, 4, 6),
                Height = 52, HorizontalAlignment = HorizontalAlignment.Center
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = dayName, FontSize = 11,
                Foreground = isSelected ? Brushes.White
                    : isWeekend ? MakeBrush("#D4687B")
                    : FindResource("TextSecondaryBrush") as Brush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = date.Day.ToString(), FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = isSelected ? Brushes.White
                    : FindResource("TextPrimaryBrush") as Brush,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0)
            });
            bd.Child = sp;

            var dot = new Border
            {
                Width = 3, Height = 3, CornerRadius = new CornerRadius(1.5),
                Background = FindResource("PrimaryBrush") as Brush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = hasTasksOnDate(date)
                    ? Visibility.Visible : Visibility.Collapsed
            };
            sp.Children.Add(dot);

            btn.Content = bd;
        }
    }

    private bool hasTasksOnDate(DateTime date)
    {
        return _allItems.Any(t => t.DueDate?.Date == date.Date && !t.IsCompleted);
    }

    private void WeekDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DateTime date) return;
        _selectedDate = date;
        _navMonth = new DateTime(date.Year, date.Month, 1);
        UpdateWeekBar();
        UpdateMonthLabel();
        ApplyFilter();
        if (_showCalendar) BuildCalendar();
    }

    #endregion

    #region Month Navigation

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _navMonth = _navMonth.AddMonths(-1);
        UpdateMonthLabel();
        UpdateWeekBarForMonth();
        if (_showCalendar) BuildCalendar();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _navMonth = _navMonth.AddMonths(1);
        UpdateMonthLabel();
        UpdateWeekBarForMonth();
        if (_showCalendar) BuildCalendar();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _selectedDate = DateTime.Today;
        _navMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        UpdateMonthLabel();
        UpdateWeekBar();
        ApplyFilter();
        if (_showCalendar) BuildCalendar();
    }

    private void UpdateMonthLabel()
    {
        MonthLabel.Text = $"{_navMonth.Year}年{_navMonth.Month}月";
        CalMonthLabel.Text = $"{_navMonth.Year}年{_navMonth.Month}月";
        var weekOfYear = System.Globalization.CultureInfo.CurrentCulture.Calendar
            .GetWeekOfYear(DateTime.Today,
                System.Globalization.CalendarWeekRule.FirstFullWeek,
                DayOfWeek.Monday);
        WeekLabel.Text = $"第{weekOfYear}周";
    }

    private void UpdateWeekBarForMonth()
    {
        if (_selectedDate.Year == _navMonth.Year && _selectedDate.Month == _navMonth.Month) return;
        _selectedDate = _navMonth;
        UpdateWeekBar();
        ApplyFilter();
    }

    #endregion

    #region Calendar View

    private void CalendarToggle_Click(object sender, RoutedEventArgs e)
    {
        _showCalendar = !_showCalendar;
        CalendarToggle.Foreground = _showCalendar
            ? FindResource("PrimaryBrush") as Brush
            : FindResource("TextSecondaryBrush") as Brush;

        if (_showCalendar)
        {
            CalMonthLabel.Text = $"{_navMonth.Year}年{_navMonth.Month}月";
            BuildCalendar();
            CalendarPopup.Visibility = Visibility.Visible;
            CalendarPopup.Opacity = 0;
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            CalendarPopup.BeginAnimation(OpacityProperty, fadeIn);
        }
        else
        {
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (_, _) => CalendarPopup.Visibility = Visibility.Collapsed;
            CalendarPopup.BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    private void CalendarPopupBg_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == CalendarPopup)
        {
            _showCalendar = false;
            CalendarToggle.Foreground = FindResource("TextSecondaryBrush") as Brush;
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (_, _) => CalendarPopup.Visibility = Visibility.Collapsed;
            CalendarPopup.BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    private void BuildCalendar()
    {
        MonthGrid.Children.Clear();
        MonthGrid.RowDefinitions.Clear();
        MonthGrid.ColumnDefinitions.Clear();

        var dayHeaders = new[] { "一", "二", "三", "四", "五", "六", "日" };
        for (int c = 0; c < 7; c++)
            MonthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        MonthGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int r = 0; r < 6; r++)
            MonthGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Day headers
        for (int c = 0; c < 7; c++)
        {
            var header = new Border
            {
                Child = new TextBlock
                {
                    Text = dayHeaders[c], FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = c >= 5 ? MakeBrush("#D4687B")
                        : FindResource("TextSecondaryBrush") as Brush,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                Padding = new Thickness(2, 6, 2, 4)
            };
            Grid.SetRow(header, 0); Grid.SetColumn(header, c);
            MonthGrid.Children.Add(header);
        }

        var firstDay = new DateTime(_navMonth.Year, _navMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(_navMonth.Year, _navMonth.Month);
        var startDow = ((int)firstDay.DayOfWeek + 6) % 7;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(_navMonth.Year, _navMonth.Month, day);
            var col = (startDow + day - 1) % 7;
            var row = 1 + (startDow + day - 1) / 7;
            var dateKey = date.ToString("yyyy-MM-dd");

            var isToday = date.Date == DateTime.Today;
            var isSelected = date.Date == _selectedDate.Date;
            var isWeekend = col >= 5;
            var tasksOnDay = _allItems.Where(t => t.DueDate?.Date == date.Date && !t.IsCompleted).ToList();
            var allTasksOnDay = _allItems.Where(t => t.DueDate?.Date == date.Date).ToList();
            var hasHoliday = Holidays.TryGetValue(dateKey, out var holidayName);

            // Build tooltip with task previews
            var dayOfWeekNames = new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            var tipText = $"{date.Month}月{date.Day}日 {dayOfWeekNames[(int)date.DayOfWeek == 0 ? 6 : (int)date.DayOfWeek - 1]}";
            if (hasHoliday) tipText += $" · {holidayName}";
            if (allTasksOnDay.Count > 0)
            {
                tipText += $"\n━━━━━━━━━━";
                foreach (var t in allTasksOnDay.Take(5))
                    tipText += $"\n{(t.IsCompleted ? "✅" : "☐")} {t.Title}";
                if (allTasksOnDay.Count > 5)
                    tipText += $"\n... 还有{allTasksOnDay.Count - 5}项";
            }
            else tipText += "\n暂无待办事项";

            // Outer cell
            var cell = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = isSelected ? FindResource("PrimaryBrush") as Brush : Brushes.Transparent,
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                ToolTip = tipText.Trim()
            };

            // Inner wrapper for padding
            var inner = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Day number
            var dayNum = new TextBlock
            {
                Text = day.ToString(),
                FontSize = 14,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isSelected ? Brushes.White
                    : isToday ? FindResource("PrimaryBrush") as Brush
                    : isWeekend ? MakeBrush("#D4687B")
                    : FindResource("TextPrimaryBrush") as Brush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            inner.Children.Add(dayNum);

            // Holiday badge
            if (hasHoliday)
            {
                var holidayBadge = new Border
                {
                    Background = MakeBrush("#FFE4D6"),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(3, 2, 3, 2),
                    Margin = new Thickness(0, 3, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                holidayBadge.Child = new TextBlock
                {
                    Text = holidayName, FontSize = 9, FontWeight = FontWeights.SemiBold,
                    Foreground = MakeBrush("#CC5500"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                inner.Children.Add(holidayBadge);
            }

            // Task dots - compact row
            var totalTasks = tasksOnDay.Count;
            if (totalTasks > 0)
            {
                var dotRow = new WrapPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    MaxWidth = 32
                };
                var maxDots = Math.Min(totalTasks, 4);
                for (int t = 0; t < maxDots; t++)
                {
                    var taskColor = string.IsNullOrEmpty(tasksOnDay[t].Color) ? "#FF8B9F" : tasksOnDay[t].Color;
                    var dot = new Border
                    {
                        Width = 4, Height = 4, CornerRadius = new CornerRadius(2),
                        Background = MakeBrush(taskColor),
                        Margin = new Thickness(1, 1, 1, 0)
                    };
                    dotRow.Children.Add(dot);
                }
                inner.Children.Add(dotRow);
            }

            // Task count
            if (totalTasks > 0)
            {
                inner.Children.Add(new TextBlock
                {
                    Text = $"{totalTasks}",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = FindResource("TextSecondaryBrush") as Brush,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            cell.Child = inner;

            // Hover effect
            cell.MouseEnter += (_, _) =>
            {
                if (!isSelected)
                    cell.Background = FindResource("SurfaceBrush") as Brush;
            };
            cell.MouseLeave += (_, _) =>
            {
                if (!isSelected)
                    cell.Background = Brushes.Transparent;
            };

            var capturedDate = date;
            cell.MouseLeftButtonDown += (s, e) =>
            {
                _selectedDate = capturedDate;
                _showCalendar = false;
                CalendarToggle.Foreground = FindResource("TextSecondaryBrush") as Brush;
                UpdateWeekBar();
                BuildCalendar();
                ApplyFilter();
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (_, _) => CalendarPopup.Visibility = Visibility.Collapsed;
                CalendarPopup.BeginAnimation(OpacityProperty, fadeOut);
            };

            if (row <= 6)
            {
                Grid.SetRow(cell, row); Grid.SetColumn(cell, col);
                MonthGrid.Children.Add(cell);
            }
        }

        UpdateCalTodayTasks();
    }

    private void UpdateCalTodayTasks()
    {
        var todayTasks = _allItems.Where(t => t.DueDate?.Date == DateTime.Today).ToList();
        CalTodayLabel.Text = todayTasks.Count > 0
            ? $"今天 · {todayTasks.Count}项任务"
            : "今天 · 无任务";
        CalTodayTasks.ItemsSource = todayTasks.Select(t => new TodoDisplayItem(t)).ToList();
    }

    #endregion

    #region Startup

    private void CheckStartupStatus()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "MomoTodo.lnk");
        UpdateStartupButton(File.Exists(path));
    }

    private void UpdateStartupButton(bool enabled)
    {
        StartupButton.Foreground = enabled
            ? FindResource("PrimaryBrush") as Brush
            : FindResource("TextSecondaryBrush") as Brush;
        StartupButton.ToolTip = enabled ? "已开机自启" : "开机自启(未启用)";
    }

    private void StartupButton_Click(object sender, RoutedEventArgs e)
    {
        var shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup), "MomoTodo.lnk");
        var exePath = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MomoTodo.exe");

        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
            UpdateStartupButton(false);
            ShowToast("🚀", "已取消开机自启", "#A7F3D0");
        }
        else
        {
            try
            {
                dynamic shell = Activator.CreateInstance(
                    Type.GetTypeFromProgID("WScript.Shell")
                    ?? throw new InvalidOperationException("WScript.Shell not found"))!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? "";
                shortcut.Description = "Momo Todo";
                shortcut.Save();
                UpdateStartupButton(true);
                ShowToast("✅", "已设置开机自启", "#A7F3D0");
            }
            catch (Exception ex) { ShowToast("⚠️", $"设置失败: {ex.Message}", "#FCD34D"); }
        }
    }

    #endregion

    #region Focus Mode

    private void FocusButton_Click(object sender, RoutedEventArgs e)
    {
        if (_focusTimer != null)
        {
            _focusTimer.Stop();
            _focusTimer = null;
            FocusButton.Content = "🧘 专注";
            FocusButton.Background = Brushes.Transparent;
            FocusButton.Foreground = FindResource("TextSecondaryBrush") as Brush;
            ShowToast("✅", "专注已结束", "#A7F3D0");
            return;
        }

        _focusMinutes = 25 * 60; // 25 minutes in seconds
        _focusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _focusTimer.Tick += (s, args) =>
        {
            _focusMinutes--;
            var min = _focusMinutes / 60;
            var sec = _focusMinutes % 60;
            FocusButton.Content = $"🧘 {min:D2}:{sec:D2}";
            FocusButton.Background = FindResource("SurfaceBrush") as Brush;

            if (_focusMinutes <= 0)
            {
                _focusTimer?.Stop();
                _focusTimer = null;
                FocusButton.Content = "🧘 专注";
                FocusButton.Background = Brushes.Transparent;
                FocusButton.Foreground = FindResource("TextSecondaryBrush") as Brush;
                ShowToast("🔔", "专注时间到！休息一下吧～", "#A7F3D0");
            }
        };
        _focusTimer.Start();
        FocusButton.Content = $"🧘 25:00";
        FocusButton.Background = FindResource("SurfaceBrush") as Brush;
        FocusButton.Foreground = FindResource("PrimaryBrush") as Brush;
        FocusButton.ToolTip = "点击停止专注";
        ShowToast("🧘", "开始专注 25 分钟", "#A78BFA");
    }

    #endregion

    #region Category Pills

    private void InitCategoryPills()
    {
        foreach (var cat in TodoItem.Categories)
        {
            var btn = new Button
            {
                Content = cat, Tag = cat, FontSize = 11, Cursor = Cursors.Hand,
                Height = 26, Margin = new Thickness(0, 0, 4, 0),
                Background = FindResource("SurfaceBrush") as Brush,
                Foreground = FindResource("TextSecondaryBrush") as Brush,
                BorderThickness = new Thickness(0)
            };
            btn.Click += CategoryFilter_Click;
            CategoryPills.Children.Add(btn);
        }
    }

    #endregion

    #region Color Picker

    private void InitColorPicker()
    {
        _colorBtns = new Border[TodoItem.Colors.Length];
        for (int i = 0; i < TodoItem.Colors.Length; i++)
        {
            var color = TodoItem.Colors[i];
            var border = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 8, 0),
                Background = i == 0
                    ? (Brush)new BrushConverter().ConvertFromString("#E8E8E8")!
                    : (Brush)new BrushConverter().ConvertFromString(color)!,
                Tag = color, Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = i == 0 ? "×" : "", FontSize = 10,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            border.MouseLeftButtonDown += ColorPicker_Click;
            ColorPicker.Items.Add(border);
            _colorBtns[i] = border;
        }
    }

    private void ColorPicker_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;
        _selectedColor = border.Tag?.ToString() ?? "";
        foreach (var b in _colorBtns)
            b.BorderThickness = new Thickness(0);
        border.BorderThickness = new Thickness(2);
        border.BorderBrush = FindResource("PrimaryBrush") as Brush;
    }

    #endregion

    #region Task Load & Filter

    private void LoadTasks()
    {
        _allItems = _todoService.GetAll();
        ApplyFilter();
        UpdateStats();
        UpdateWeekBar();
        if (_showCalendar) BuildCalendar();
        RefreshQuote();
    }

    private void ApplyFilter()
    {
        var query = _allItems.AsEnumerable();

        // Filter by selected date
        query = query.Where(t =>
            t.DueDate?.Date == null || t.DueDate.Value.Date == _selectedDate.Date);

        // Status filter
        query = _currentFilter switch
        {
            "active" => query.Where(t => !t.IsCompleted),
            "done" => query.Where(t => t.IsCompleted),
            "overdue" => query.Where(t => t.IsOverdue),
            _ => query
        };

        // Category
        if (!string.IsNullOrEmpty(_activeCategory))
            query = query.Where(t => t.Category == _activeCategory);

        // Search
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var s = _searchText.ToLower();
            query = query.Where(t =>
                t.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (t.Description?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var filtered = query.OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedAt).ToList();

        TaskList.ItemsSource = filtered.Select(t => new TodoDisplayItem(t)).ToList();
        TaskScroll.ScrollToTop();
    }

    private void UpdateStats()
    {
        var today = _allItems.Count(t => t.DueDate?.Date == DateTime.Today);
        var total = _allItems.Count;
        TodayCount.Text = $"今 {today}";
        TotalCount.Text = $"总 {total}";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        ApplyFilter();
    }

    private void CategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _activeCategory = btn.Tag?.ToString() ?? "";
        // Update active state
        foreach (var child in CategoryPills.Children)
        {
            if (child is Button b)
            {
                b.Background = FindResource("SurfaceBrush") as Brush;
                b.Foreground = FindResource("TextSecondaryBrush") as Brush;
            }
        }
        btn.Background = FindResource("PrimaryBrush") as Brush;
        btn.Foreground = Brushes.White;
        ApplyFilter();
    }

    private void RefreshQuote()
    {
        var (content, author) = _todoService.GetRandomQuote();
        QuoteText.Text = $"「{content}」";
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadTasks();
        RefreshQuote();
    }

    #endregion

    #region Task Card Actions

    private void TaskCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not int id) return;
        var item = _allItems.FirstOrDefault(t => t.Id == id);
        if (item == null) return;
        item.IsCompleted = cb.IsChecked == true;
        _todoService.Update(item);

        if (cb.IsChecked == true)
        {
            ShowToast("✅", "任务已完成，好棒！", "#A7F3D0");
            var border = FindVisualParent<Border>(cb);
            if (border != null)
            {
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (_, _) => { if (_currentFilter == "active") LoadTasks(); };
                border.BeginAnimation(OpacityProperty, fadeOut);
            }
            else { if (_currentFilter == "active") LoadTasks(); }
        }
        else { LoadTasks(); }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null) { if (parent is T t) return t; parent = VisualTreeHelper.GetParent(parent); }
        return null;
    }

    private void TaskCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is Border border && border.Tag is int id)
        {
            var item = _allItems.FirstOrDefault(t => t.Id == id);
            if (item != null) ShowEditDialog(item);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int id) return;
        var item = _allItems.FirstOrDefault(t => t.Id == id);
        if (item != null) ShowEditDialog(item);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int id) return;
        var item = _allItems.FirstOrDefault(t => t.Id == id);
        if (item == null) return;
        _pendingDeleteId = id;
        ShowConfirm("确认删除", $"确定要删除「{item.Title}」吗？\n不可撤销哦～");
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        ShowEditDialog(null);
        var quickText = QuickAddBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(quickText))
            EditTitleBox.Text = quickText;
    }

    private void QuickAdd_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var text = QuickAddBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            _todoService.Add(new TodoItem
            {
                Title = text,
                Category = _activeCategory != "" ? _activeCategory : "📋 一般",
                DueDate = _selectedDate.Date
            });
            QuickAddBox.Text = "";
            LoadTasks();
            ShowToast("✅", "已快速添加", "#A7F3D0");
        }
    }

    #endregion

    #region Edit Dialog

    private void ShowEditDialog(TodoItem? existing)
    {
        _editingId = existing?.Id > 0 ? existing.Id : null;
        EditTitle.Text = existing == null ? "新建任务" : "编辑任务";
        EditTitleBox.Text = existing?.Title ?? "";
        EditDescBox.Text = existing?.Description ?? "";
        EditDuePicker.SelectedDate = existing?.DueDate ?? DateTime.Today;
        EditCategoryBox.SelectedIndex = Math.Max(0,
            Array.IndexOf(TodoItem.Categories, existing?.Category ?? "📋 一般"));
        EditPriorityBox.SelectedIndex = Math.Clamp((existing?.Priority ?? 1) - 1, 0, 2);

        // Color
        _selectedColor = existing?.Color ?? "";
        foreach (var b in _colorBtns)
        {
            b.BorderThickness = new Thickness(b.Tag?.ToString() == _selectedColor ? 2 : 0);
            b.BorderBrush = FindResource("PrimaryBrush") as Brush;
        }

        // Repeat
        EditRepeatBox.SelectedIndex = Math.Max(0,
            Array.IndexOf(TodoItem.Repeats, existing?.Repeat ?? "None"));

        // Defer
        EditDeferBox.SelectedIndex = Math.Max(0,
            Array.IndexOf(TodoItem.Defers, existing?.Defer ?? "Default"));

        // Reminder
        if (existing?.ReminderTime != null)
        {
            ReminderToggle.IsChecked = true;
            EditReminderDate.SelectedDate = existing.ReminderTime.Value.Date;
            EditReminderHour.SelectedIndex = existing.ReminderTime.Value.Hour;
            EditReminderMin.SelectedIndex = existing.ReminderTime.Value.Minute / 5;
        }
        else
        {
            ReminderToggle.IsChecked = false;
            EditReminderDate.SelectedDate = DateTime.Today;
            EditReminderHour.SelectedIndex = 8;
            EditReminderMin.SelectedIndex = 0;
        }

        EditOverlay.Visibility = Visibility.Visible;
        // Reset preview state
        _descPreview = false;
        EditDescBox.Visibility = Visibility.Visible;
        DescPreview.Visibility = Visibility.Collapsed;
        DescPreviewToggle.Content = "预览";
        DescPreviewToggle.Background = FindResource("SurfaceBrush") as Brush;
        DescPreviewToggle.Foreground = FindResource("TextSecondaryBrush") as Brush;
        EditTitleBox.Focus();
    }

    private void EditOverlayBg_Click(object sender, MouseButtonEventArgs e)
    {
        // Only close if clicking on the overlay background, not the dialog
        if (e.OriginalSource == EditOverlay)
            HideEditDialog();
    }

    public static Brush MakeBrush(string hex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch { return Brushes.Transparent; }
    }

    private bool _descPreview;
    private void EditCancel_Click(object sender, RoutedEventArgs e) => HideEditDialog();

    private void DescPreviewToggle_Click(object sender, RoutedEventArgs e)
    {
        _descPreview = !_descPreview;
        if (_descPreview)
        {
            // Switch to preview
            EditDescBox.Visibility = Visibility.Collapsed;
            var mdView = MarkdownRenderer.Render(EditDescBox.Text);
            DescPreview.Content = mdView;
            DescPreview.Visibility = Visibility.Visible;
            DescPreviewToggle.Content = "编辑";
            DescPreviewToggle.Background = FindResource("PrimaryBrush") as Brush;
            DescPreviewToggle.Foreground = Brushes.White;
        }
        else
        {
            EditDescBox.Visibility = Visibility.Visible;
            DescPreview.Visibility = Visibility.Collapsed;
            DescPreviewToggle.Content = "预览";
            DescPreviewToggle.Background = FindResource("SurfaceBrush") as Brush;
            DescPreviewToggle.Foreground = FindResource("TextSecondaryBrush") as Brush;
        }
    }

    private void HideEditDialog()
    {
        EditOverlay.Visibility = Visibility.Collapsed;
        _editingId = null;
    }

    private void EditToday_Click(object sender, RoutedEventArgs e)
    {
        EditDuePicker.SelectedDate = DateTime.Today;
    }

    private void EditSave_Click(object sender, RoutedEventArgs e)
    {
        if (_saving) return;
        _saving = true;

        try
        {
            var title = EditTitleBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                ShowToast("⚠️", "请至少输入一个任务名称～", "#FCD34D");
                EditTitleBox.Focus();
                return;
            }

            var catIdx = EditCategoryBox.SelectedIndex;
            var category = catIdx >= 0 && catIdx < TodoItem.Categories.Length
                ? TodoItem.Categories[catIdx] : "📋 一般";

            var item = new TodoItem
            {
                Id = _editingId ?? 0,
                Title = title,
                Description = string.IsNullOrWhiteSpace(EditDescBox.Text) ? null : EditDescBox.Text.Trim(),
                Category = category,
                Priority = EditPriorityBox.SelectedIndex + 1,
                DueDate = EditDuePicker.SelectedDate?.Date,
                ReminderTime = BuildReminder(),
                Color = _selectedColor,
                Repeat = TodoItem.Repeats[Math.Clamp(EditRepeatBox.SelectedIndex, 0, 3)],
                Defer = TodoItem.Defers[Math.Clamp(EditDeferBox.SelectedIndex, 0, 2)]
            };

            bool isEdit = _editingId.HasValue;
            if (isEdit)
                _todoService.Update(item);
            else
                _todoService.Add(item);

            // Handle defer: auto-defer moves undone task to tomorrow
            if (item.Defer == "AutoDefer" && item.DueDate?.Date == DateTime.Today && !item.IsCompleted)
            {
                // Task not completed today - already handled by due date logic
            }

            HideEditDialog();
            LoadTasks();
            ShowToast("✅", isEdit ? "已更新成功" : "已添加新任务", "#A7F3D0");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败:\n{ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            HideEditDialog();
        }
        finally { _saving = false; }
    }

    private void ReminderToggle_Changed(object sender, RoutedEventArgs e)
    {
        ReminderPanel.Visibility = ReminderToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FormQuickReminder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag == null) return;
        var tag = btn.Tag.ToString()!;
        DateTime reminderTime;
        if (tag == "tomorrow9") reminderTime = DateTime.Today.AddDays(1).AddHours(9);
        else if (int.TryParse(tag, out var min)) reminderTime = DateTime.Now.AddMinutes(min);
        else return;
        EditReminderDate.SelectedDate = reminderTime.Date;
        EditReminderHour.SelectedIndex = reminderTime.Hour;
        EditReminderMin.SelectedIndex = reminderTime.Minute / 5;
    }

    private DateTime? BuildReminder()
    {
        if (ReminderToggle.IsChecked != true) return null;
        try
        {
            if (EditReminderHour.SelectedItem is not ComboBoxItem hi
                || EditReminderMin.SelectedItem is not ComboBoxItem mi) return null;
            var date = EditReminderDate.SelectedDate ?? DateTime.Today;
            if (!int.TryParse(hi.Content?.ToString(), out var h)
                || !int.TryParse(mi.Content?.ToString(), out var m)) return null;
            return date.AddHours(h).AddMinutes(m);
        }
        catch { return null; }
    }

    #endregion

    #region Pin

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Foreground = Topmost
            ? FindResource("PrimaryBrush") as Brush
            : FindResource("TextSecondaryBrush") as Brush;
        PinButton.ToolTip = Topmost ? "已置顶" : "置顶窗口";
    }

    #endregion

    #region Toast & Confirm

    private void ShowToast(string icon, string text, string bgColor)
    {
        ToastIcon.Text = icon;
        ToastText.Text = text;
        ToastPanel.Background = (Brush)new BrushConverter().ConvertFromString(bgColor)!;
        ToastPanel.Opacity = 0;
        ToastPanel.Visibility = Visibility.Visible;

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
        ToastPanel.BeginAnimation(OpacityProperty, fadeIn);

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer?.Stop();
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (_, _) => ToastPanel.Visibility = Visibility.Collapsed;
            ToastPanel.BeginAnimation(OpacityProperty, fadeOut);
        };
        _toastTimer.Start();
    }

    private void ShowConfirm(string title, string text)
    {
        ConfirmTitle.Text = title;
        ConfirmText.Text = text;
        ConfirmPanel.Visibility = Visibility.Visible;
    }

    private void ConfirmCancel_Click(object sender, RoutedEventArgs e)
    {
        ConfirmPanel.Visibility = Visibility.Collapsed;
        _pendingDeleteId = null;
    }

    private void ConfirmOk_Click(object sender, RoutedEventArgs e)
    {
        ConfirmPanel.Visibility = Visibility.Collapsed;
        if (_pendingDeleteId.HasValue)
        {
            _todoService.Delete(_pendingDeleteId.Value);
            _pendingDeleteId = null;
            LoadTasks();
            ShowToast("🗑️", "已删除成功", "#EF4444");
        }
    }

    #endregion

    #region Keyboard

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (EditOverlay.Visibility == Visibility.Visible)
                HideEditDialog();
            else if (CalendarPopup.Visibility == Visibility.Visible)
            {
                _showCalendar = false;
                CalendarToggle.Foreground = FindResource("TextSecondaryBrush") as Brush;
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (_, _) => CalendarPopup.Visibility = Visibility.Collapsed;
                CalendarPopup.BeginAnimation(OpacityProperty, fadeOut);
            }
            else if (ConfirmPanel.Visibility == Visibility.Visible)
                ConfirmCancel_Click(sender, e);
        }
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        _focusTimer?.Stop();
        _reminderService.Dispose();
        _todoService.Dispose();
        base.OnClosed(e);
    }
}

public class TodoDisplayItem : INotifyPropertyChanged
{
    private readonly TodoItem _item;
    public TodoDisplayItem(TodoItem item) => _item = item;
    public int Id => _item.Id;
    public string Title => _item.Title;
    public string? Description => _item.Description;
    public string Category => _item.Category;
    public bool IsCompleted { get => _item.IsCompleted; set { _item.IsCompleted = value; OnPropertyChanged(); } }
    public string PriorityLabel => _item.Priority switch { 3 => "🔴", 2 => "🟡", _ => "⚪" };
    public string DueDateFormatted => _item.DueDate?.ToString("MM-dd HH:mm") ?? "无截止日";
    public string ReminderFormatted => _item.ReminderTime?.ToString("MM-dd HH:mm") ?? "无提醒";
    public string ColorName => string.IsNullOrEmpty(_item.Color) ? "无"
        : TodoItem.ColorNames[Math.Clamp(Array.IndexOf(TodoItem.Colors, _item.Color), 0, 7)];
    public Brush ColorBrush
    {
        get
        {
            if (string.IsNullOrEmpty(_item.Color)) return Brushes.Transparent;
            return MainWindow.MakeBrush(_item.Color);
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class GroupHeader
{
    public string Label { get; set; } = "";
}
