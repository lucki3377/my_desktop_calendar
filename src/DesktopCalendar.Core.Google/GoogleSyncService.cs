using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;

namespace DesktopCalendar.Core.Google;

/// <summary>동기화 1회의 결과. 실패해도 예외 대신 이 값으로 알린다(위젯이 조용히 무시할 수 있게).</summary>
public sealed record GoogleSyncResult(
    bool Success,
    string Message,
    int EventCount,
    DateTime RangeStart,
    DateTime RangeEnd)
{
    public static GoogleSyncResult Skipped(string message) =>
        new(false, message, 0, DateTime.MinValue, DateTime.MinValue);

    public static GoogleSyncResult Failed(string message) => Skipped(message);
}

/// <summary>
/// 구글 캘린더 → 로컬 캐시 폴링 동기화 (DESIGN.md 4.4).
/// 기준 달 앞뒤 <see cref="GoogleSettings.SyncMonthsAround"/>개월 구간을 받아와 캐시를 통째로 교체한다.
/// </summary>
public sealed class GoogleSyncService(
    GoogleSettings settings,
    GoogleEventRepository repository,
    IDataStore dataStore)
{
    public async Task<GoogleSyncResult> SyncAsync(DateTime anchorMonth, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
            return GoogleSyncResult.Skipped("구글 OAuth 클라이언트 정보가 설정되지 않았습니다.");

        if (!settings.IsConnected)
            return GoogleSyncResult.Skipped("구글 계정이 연결되지 않았습니다.");

        var calendarIds = settings.CalendarIds;
        if (calendarIds.Count == 0)
        {
            repository.Clear();
            return GoogleSyncResult.Skipped("연동할 캘린더가 선택되지 않았습니다.");
        }

        var monthStart = new DateTime(anchorMonth.Year, anchorMonth.Month, 1);
        var rangeStart = monthStart.AddMonths(-GoogleSettings.SyncMonthsAround);
        var rangeEnd = monthStart.AddMonths(GoogleSettings.SyncMonthsAround + 1);

        var client = new GoogleCalendarClient(settings.ClientId!, settings.ClientSecret!, dataStore);

        try
        {
            var credential = await client.TryRestoreCredentialAsync();
            if (credential is null)
                return GoogleSyncResult.Failed("저장된 인증 정보가 없습니다. 설정에서 구글 계정을 다시 연결하세요.");

            var events = await client.GetEventsAsync(credential, calendarIds, rangeStart, rangeEnd, cancellationToken);
            repository.ReplaceAll(events);
            settings.LastSyncedAt = DateTime.Now;

            return new GoogleSyncResult(true, $"{events.Count}개 일정을 가져왔습니다.", events.Count, rangeStart, rangeEnd);
        }
        catch (TokenResponseException ex)
        {
            return GoogleSyncResult.Failed($"구글 인증이 만료되었습니다. 설정에서 다시 연결하세요. ({ex.Error?.Error})");
        }
        catch (global::Google.GoogleApiException ex)
        {
            return GoogleSyncResult.Failed($"구글 캘린더 조회에 실패했습니다: {ex.Error?.Message ?? ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return GoogleSyncResult.Failed($"네트워크 오류로 동기화하지 못했습니다: {ex.Message}");
        }
    }
}
