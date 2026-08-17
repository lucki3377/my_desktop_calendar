using System.IO;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core.Calendar;
using DesktopCalendar.Core.Google;
using DesktopCalendar.Core.Holiday;
using DesktopCalendar.Core.Storage;
using Google.Apis.Util.Store;

namespace DesktopCalendar.App;

/// <summary>
/// 모든 설정을 한 곳에 모은 창 (DESIGN.md Phase 6).
/// 우클릭 메뉴에 항목이 열 개 넘게 늘어나 흩어져 있던 것을 정리했다.
/// 값을 바꾸면 곧바로 저장하고 <see cref="SettingsChanged"/>로 위젯에 알린다 — 확인/취소가 따로 없다.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly GoogleSettings _googleSettings;
    private readonly ScheduleRepository _scheduleRepository;
    private readonly DDayRepository _dDayRepository;
    private readonly HolidayRepository _holidayRepository;
    private readonly IDataStore _googleDataStore;
    private readonly GoogleEventRepository _googleEventRepository;

    private bool _isLoading = true;

    /// <summary>표시 설정이 바뀌어 위젯을 다시 그려야 할 때 발생한다.</summary>
    public event EventHandler? SettingsChanged;

    public SettingsWindow(
        SettingsStore settings,
        GoogleSettings googleSettings,
        ScheduleRepository scheduleRepository,
        DDayRepository dDayRepository,
        HolidayRepository holidayRepository,
        GoogleEventRepository googleEventRepository,
        IDataStore googleDataStore)
    {
        InitializeComponent();

        _settings = settings;
        _googleSettings = googleSettings;
        _scheduleRepository = scheduleRepository;
        _dDayRepository = dDayRepository;
        _holidayRepository = holidayRepository;
        _googleEventRepository = googleEventRepository;
        _googleDataStore = googleDataStore;

        LoadValues();
        _isLoading = false;
    }

    private void LoadValues()
    {
        FontScaleSlider.Value = Math.Round(_settings.GetDouble("Widget.FontScale", 1.0) * 100);
        MondayStartCheckBox.IsChecked = _settings.GetBool("Widget.WeekStartsOnMonday", false);
        LunarCheckBox.IsChecked = _settings.GetBool("Widget.ShowLunarDates", false);
        ShowGoogleCheckBox.IsChecked = _googleSettings.ShowEvents;
        AutoStartCheckBox.IsChecked = AutoStartService.IsEnabled;

        AutoStartNoteText.Text = $"시작프로그램 폴더에 바로가기를 만듭니다: {AutoStartService.GetStartupFolderPath()}";

        UpdateSummaries();
    }

    /// <summary>버튼으로 여는 창들에서 값이 바뀌었을 수 있으므로 요약을 다시 읽는다.</summary>
    private void UpdateSummaries()
    {
        FontScaleText.Text = $"{(int)FontScaleSlider.Value}%";

        var theme = WidgetTheme.Load(_settings);
        ThemeSummaryText.Text = $"{WidgetTheme.ToHex(theme.PanelColor)} · 불투명도 {theme.Opacity * 100:0}%";

        HolidayStatusText.Text = string.IsNullOrWhiteSpace(_settings.GetString("Holiday.ApiKey"))
            ? "키 없음 — 앱이 직접 계산한 공휴일을 씁니다 (임시공휴일 제외)"
            : "키 설정됨 — 임시공휴일까지 자동으로 받아옵니다";

        GoogleStatusText.Text = _googleSettings.IsConnected
            ? $"{_googleSettings.AccountEmail} · 캘린더 {_googleSettings.CalendarIds.Count}개"
            : "연결 안 됨";

        DDayStatusText.Text = $"등록된 D-day {_dDayRepository.GetAll().Count}개";
    }

    private void NotifyChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    private void FontScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || !IsInitialized)
            return;

        FontScaleText.Text = $"{(int)FontScaleSlider.Value}%";
        _settings.SetDouble("Widget.FontScale", FontScaleSlider.Value / 100.0);
        NotifyChanged();
    }

    private void DisplayOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        _settings.SetBool("Widget.WeekStartsOnMonday", MondayStartCheckBox.IsChecked == true);
        _settings.SetBool("Widget.ShowLunarDates", LunarCheckBox.IsChecked == true);
        NotifyChanged();
    }

    private void ShowGoogleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        _googleSettings.ShowEvents = ShowGoogleCheckBox.IsChecked == true;
        NotifyChanged();
    }

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        var wanted = AutoStartCheckBox.IsChecked == true;
        try
        {
            AutoStartService.SetEnabled(wanted);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show($"자동 실행 설정을 바꾸지 못했습니다: {ex.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Warning);

            // 실제 상태와 화면이 어긋나지 않게 되돌린다.
            _isLoading = true;
            AutoStartCheckBox.IsChecked = AutoStartService.IsEnabled;
            _isLoading = false;
        }
    }

    private void AppearanceButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AppearanceDialog(WidgetTheme.Load(_settings)) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            dialog.SelectedTheme.Save(_settings);
            UpdateSummaries();
            NotifyChanged();
        }
    }

    private void ApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ApiKeyDialog(_settings.GetString("Holiday.ApiKey")) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ApiKey is not null)
        {
            _settings.SetString("Holiday.ApiKey", dialog.ApiKey);
            UpdateSummaries();
            NotifyChanged();
        }
    }

    private void GoogleButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new GoogleSettingsWindow(_googleSettings, _googleEventRepository, _googleDataStore)
        {
            Owner = this,
        };
        window.ShowDialog();

        _isLoading = true;
        ShowGoogleCheckBox.IsChecked = _googleSettings.ShowEvents;
        _isLoading = false;

        UpdateSummaries();
        NotifyChanged();
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        var backupService = new BackupService(_scheduleRepository, _dDayRepository, _holidayRepository);
        var window = new BackupWindow(backupService) { Owner = this };
        window.ShowDialog();

        if (window.DataChanged)
        {
            UpdateSummaries();
            NotifyChanged();
        }
    }

    private void DDayButton_Click(object sender, RoutedEventArgs e)
    {
        new DDayListWindow(_dDayRepository) { Owner = this }.ShowDialog();
        UpdateSummaries();
        NotifyChanged();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e) =>
        new HelpWindow() { Owner = this }.ShowDialog();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
