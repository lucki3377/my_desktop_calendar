using System.Globalization;
using System.Net.Http;
using System.Windows;
using DesktopCalendar.Core.Google;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;

namespace DesktopCalendar.App;

/// <summary>
/// 구글 캘린더 연동 설정 창 (DESIGN.md Phase 5).
/// OAuth 클라이언트 입력 → 계정 연결 → 캘린더 선택 → 표시/동기화 옵션까지 한 곳에서 처리한다.
/// </summary>
public partial class GoogleSettingsWindow : Window
{
    /// <summary>OAuth 동의 창을 무한정 기다리지 않도록 하는 제한 시간.</summary>
    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromMinutes(3);

    private readonly GoogleSettings _settings;
    private readonly GoogleEventRepository _eventRepository;
    private readonly IDataStore _dataStore;

    private readonly List<CalendarSelection> _calendars = [];

    public GoogleSettingsWindow(GoogleSettings settings, GoogleEventRepository eventRepository, IDataStore dataStore)
    {
        InitializeComponent();

        _settings = settings;
        _eventRepository = eventRepository;
        _dataStore = dataStore;

        ClientIdBox.Text = _settings.ClientId ?? string.Empty;
        ClientSecretBox.Text = _settings.ClientSecret ?? string.Empty;
        ShowEventsCheckBox.IsChecked = _settings.ShowEvents;
        SyncIntervalBox.Text = _settings.SyncIntervalMinutes.ToString(CultureInfo.InvariantCulture);

        UpdateConnectionStatus();
        UpdateSyncStatus();
        ShowSavedCalendarsAsPlaceholder();

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_settings.IsConnected)
            await RefreshCalendarListAsync(showErrors: false);
    }

    private void UpdateConnectionStatus()
    {
        ConnectionStatusText.Text = _settings.IsConnected
            ? $"연결됨: {_settings.AccountEmail}"
            : "연결 상태: 연결 안 됨";

        ConnectButton.Content = _settings.IsConnected ? "다시 연결" : "구글 계정 연결";
        DisconnectButton.IsEnabled = _settings.IsConnected;
    }

    private void UpdateSyncStatus(string? extraMessage = null)
    {
        var lastSynced = _settings.LastSyncedAt;
        var baseText = lastSynced is null
            ? "마지막 동기화: 없음"
            : $"마지막 동기화: {lastSynced.Value:yyyy-MM-dd HH:mm}";

        SyncStatusText.Text = extraMessage is null ? baseText : $"{baseText} — {extraMessage}";
    }

    /// <summary>
    /// 캘린더 목록을 아직 못 받아왔을 때, 저장된 ID만이라도 보여줘 선택 상태가 비어 보이지 않게 한다.
    /// </summary>
    private void ShowSavedCalendarsAsPlaceholder()
    {
        _calendars.Clear();
        foreach (var id in _settings.CalendarIds)
            _calendars.Add(new CalendarSelection(id, id) { IsSelected = true });

        CalendarListBox.ItemsSource = null;
        CalendarListBox.ItemsSource = _calendars;
    }

    /// <summary>구글에서 캘린더 목록을 받아와 체크박스 목록을 갱신한다(저장된 선택 상태는 유지).</summary>
    private async Task RefreshCalendarListAsync(bool showErrors)
    {
        if (!_settings.IsConfigured)
            return;

        var client = new GoogleCalendarClient(_settings.ClientId!, _settings.ClientSecret!, _dataStore);

        try
        {
            var credential = await client.TryRestoreCredentialAsync();
            if (credential is null)
            {
                if (showErrors)
                    MessageBox.Show("저장된 인증 정보가 없습니다. '구글 계정 연결'을 먼저 눌러주세요.",
                        "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var calendars = await client.GetCalendarsAsync(credential);
            var selectedIds = _settings.CalendarIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

            _calendars.Clear();
            foreach (var calendar in calendars)
            {
                var display = calendar.IsPrimary ? $"{calendar.Summary} (기본)" : calendar.Summary;
                _calendars.Add(new CalendarSelection(calendar.Id, display)
                {
                    IsSelected = selectedIds.Contains(calendar.Id),
                });
            }

            CalendarListBox.ItemsSource = null;
            CalendarListBox.ItemsSource = _calendars;
        }
        catch (Exception ex) when (ex is TokenResponseException or global::Google.GoogleApiException or HttpRequestException)
        {
            if (showErrors)
                MessageBox.Show($"캘린더 목록을 가져오지 못했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var clientId = ClientIdBox.Text.Trim();
        var clientSecret = ClientSecretBox.Text.Trim();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            MessageBox.Show("클라이언트 ID와 보안 비밀을 먼저 입력하세요.",
                "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 인증 플로우가 저장된 클라이언트 정보를 쓰므로 먼저 저장한다.
        _settings.ClientId = clientId;
        _settings.ClientSecret = clientSecret;

        SetBusy(true, "브라우저에서 구글 로그인/동의를 완료하세요...");

        try
        {
            var client = new GoogleCalendarClient(clientId, clientSecret, _dataStore);
            using var cts = new CancellationTokenSource(AuthorizationTimeout);

            var credential = await client.AuthorizeAsync(cts.Token);
            var email = await client.GetAccountEmailAsync(credential, cts.Token);

            _settings.AccountEmail = email ?? "(알 수 없는 계정)";
            UpdateConnectionStatus();

            await RefreshCalendarListAsync(showErrors: true);

            // 처음 연결한 것이라면 기본 캘린더를 자동으로 켜준다.
            if (_settings.CalendarIds.Count == 0 && _calendars.Count > 0)
                _calendars[0].IsSelected = true;

            CalendarListBox.Items.Refresh();

            MessageBox.Show($"'{_settings.AccountEmail}' 계정이 연결되었습니다.\n연동할 캘린더를 선택한 뒤 저장하세요.",
                "연결 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show("시간 안에 인증이 끝나지 않아 취소했습니다. 다시 시도하세요.",
                "인증 취소", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) when (ex is TokenResponseException or global::Google.GoogleApiException or HttpRequestException)
        {
            MessageBox.Show($"구글 계정 연결에 실패했습니다: {ex.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
            UpdateSyncStatus();
        }
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "구글 계정 연결을 해제하고 가져온 일정 캐시를 삭제할까요?",
            "연결 해제", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        SetBusy(true, "연결 해제 중...");

        try
        {
            if (_settings.IsConfigured)
            {
                var client = new GoogleCalendarClient(_settings.ClientId!, _settings.ClientSecret!, _dataStore);
                await client.DisconnectAsync();
            }
            else
            {
                await _dataStore.ClearAsync();
            }
        }
        catch (Exception ex) when (ex is TokenResponseException or global::Google.GoogleApiException or HttpRequestException)
        {
            // 서버 폐기에 실패해도 로컬 정리는 계속 진행한다.
            MessageBox.Show($"구글 서버의 토큰 폐기에 실패했지만 로컬 정보는 삭제합니다: {ex.Message}",
                "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _eventRepository.Clear();
            _settings.AccountEmail = null;
            _settings.CalendarIds = [];
            _settings.LastSyncedAt = null;

            _calendars.Clear();
            CalendarListBox.ItemsSource = null;
            CalendarListBox.ItemsSource = _calendars;

            SetBusy(false);
            UpdateConnectionStatus();
            UpdateSyncStatus();
        }
    }

    private async void SyncNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPersistSelections())
            return;

        SetBusy(true, "동기화 중...");
        try
        {
            var syncService = new GoogleSyncService(_settings, _eventRepository, _dataStore);
            var result = await syncService.SyncAsync(DateTime.Today);

            UpdateSyncStatus(result.Message);
            if (!result.Success)
                MessageBox.Show(result.Message, "동기화", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPersistSelections())
            return;

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>화면의 입력값을 설정에 반영한다. 값이 잘못됐으면 안내 후 false.</summary>
    private bool TryPersistSelections()
    {
        if (!int.TryParse(SyncIntervalBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval)
            || interval < 1)
        {
            MessageBox.Show("동기화 주기는 1 이상의 정수(분)로 입력하세요.",
                "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _settings.ClientId = ClientIdBox.Text.Trim();
        _settings.ClientSecret = ClientSecretBox.Text.Trim();
        _settings.ShowEvents = ShowEventsCheckBox.IsChecked == true;
        _settings.SyncIntervalMinutes = interval;
        _settings.CalendarIds = [.. _calendars.Where(c => c.IsSelected).Select(c => c.Id)];

        return true;
    }

    private void SetBusy(bool isBusy, string? message = null)
    {
        ConnectButton.IsEnabled = !isBusy;
        DisconnectButton.IsEnabled = !isBusy && _settings.IsConnected;
        SyncNowButton.IsEnabled = !isBusy;
        SaveButton.IsEnabled = !isBusy;

        Cursor = isBusy ? System.Windows.Input.Cursors.Wait : null;

        if (message is not null)
            SyncStatusText.Text = message;
    }

    /// <summary>캘린더 체크박스 한 줄. CheckBox가 IsSelected에 TwoWay로 값을 써 넣는다.</summary>
    public sealed class CalendarSelection(string id, string display)
    {
        public string Id { get; } = id;
        public string Display { get; } = display;
        public bool IsSelected { get; set; }
    }
}
