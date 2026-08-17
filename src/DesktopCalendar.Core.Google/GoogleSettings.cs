using System.Globalization;
using System.Text.Json;
using DesktopCalendar.Core.Storage;

namespace DesktopCalendar.Core.Google;

/// <summary>
/// 구글 연동 관련 설정을 <see cref="SettingsStore"/> 위에 타입 있게 감싼 뷰 (DESIGN.md 4.5, 4.8).
/// Client Secret은 평문으로 두지 않고 DPAPI로 암호화해서 넣는다.
/// </summary>
public sealed class GoogleSettings(SettingsStore settings)
{
    public const int DefaultSyncIntervalMinutes = 30;

    /// <summary>동기화해 올 기간 — 표시 중인 달 기준 앞뒤로 몇 달까지 받아올지 (DESIGN.md 4.4: 이번달 ±1개월).</summary>
    public const int SyncMonthsAround = 1;

    public string? ClientId
    {
        get => settings.GetString("Google.ClientId");
        set => settings.SetString("Google.ClientId", value ?? string.Empty);
    }

    public string? ClientSecret
    {
        get => TokenProtector.UnprotectFromBase64(settings.GetString("Google.ClientSecret"));
        set => settings.SetString("Google.ClientSecret",
            string.IsNullOrEmpty(value) ? string.Empty : TokenProtector.ProtectToBase64(value));
    }

    /// <summary>연결된 구글 계정 이메일. 값이 있으면 "연결됨"으로 취급한다.</summary>
    public string? AccountEmail
    {
        get => NullIfBlank(settings.GetString("Google.AccountEmail"));
        set => settings.SetString("Google.AccountEmail", value ?? string.Empty);
    }

    public IReadOnlyList<string> CalendarIds
    {
        get
        {
            var raw = settings.GetString("Google.CalendarIds");
            if (string.IsNullOrWhiteSpace(raw))
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<string>>(raw) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
        set => settings.SetString("Google.CalendarIds", JsonSerializer.Serialize(value));
    }

    public bool ShowEvents
    {
        get => settings.GetBool("Google.ShowEvents", true);
        set => settings.SetBool("Google.ShowEvents", value);
    }

    public int SyncIntervalMinutes
    {
        get
        {
            var value = (int)settings.GetDouble("Google.SyncIntervalMinutes", DefaultSyncIntervalMinutes);
            return value < 1 ? DefaultSyncIntervalMinutes : value;
        }
        set => settings.SetDouble("Google.SyncIntervalMinutes", value);
    }

    public DateTime? LastSyncedAt
    {
        get
        {
            var raw = settings.GetString("Google.LastSyncedAt");
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
                ? value
                : null;
        }
        set => settings.SetString("Google.LastSyncedAt",
            value?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty);
    }

    /// <summary>OAuth 클라이언트 정보가 입력되어 있는지.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>계정 연결까지 끝났는지.</summary>
    public bool IsConnected => IsConfigured && AccountEmail is not null;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
