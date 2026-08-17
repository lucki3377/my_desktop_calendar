using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace DesktopCalendar.App;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private WidgetWindow? _widget;
    private ReminderService? _reminderService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 위젯을 닫아도 트레이 아이콘으로 계속 살아 있어야 하므로 자동 종료를 끈다.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _widget = new WidgetWindow();
        _widget.Show();

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");

        _reminderService = new ReminderService(_trayIcon);
        _reminderService.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _reminderService?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private void TrayIcon_LeftMouseUp(object sender, RoutedEventArgs e) => ToggleWidget();

    private void TrayToggleWidget_Click(object sender, RoutedEventArgs e) => ToggleWidget();

    /// <summary>
    /// 위젯을 잠시 치우고 싶을 때를 위한 토글. 창을 닫아버리면 WorkerW 부착을 다시 해야 하므로
    /// 숨기기/보이기만 한다.
    /// </summary>
    private void ToggleWidget()
    {
        if (_widget is null)
            return;

        _widget.Visibility = _widget.Visibility == Visibility.Visible
            ? Visibility.Hidden
            : Visibility.Visible;
    }

    private void TrayAppearance_Click(object sender, RoutedEventArgs e) => _widget?.OpenAppearanceDialog();

    private void TrayHelp_Click(object sender, RoutedEventArgs e) => new HelpWindow().ShowDialog();

    private void TrayExit_Click(object sender, RoutedEventArgs e) => Shutdown();
}
