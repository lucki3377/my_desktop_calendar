using System.Globalization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace DesktopCalendar.Core.Google;

/// <summary>
/// 구글 캘린더 OAuth 인증 + 조회 클라이언트 (DESIGN.md 4.4).
/// 읽기 전용 스코프만 요청한다 — 로컬 일정을 구글로 올리지 않는 단방향 설계.
/// </summary>
public sealed class GoogleCalendarClient(string clientId, string clientSecret, IDataStore dataStore)
{
    private const string ApplicationName = "DesktopCalendar";

    /// <summary>DataStore에 토큰을 넣을 때 쓰는 사용자 키. 계정은 하나만 지원한다.</summary>
    private const string UserKey = "user";

    private static readonly string[] Scopes = [CalendarService.Scope.CalendarReadonly];

    private ClientSecrets Secrets => new() { ClientId = clientId, ClientSecret = clientSecret };

    /// <summary>
    /// 브라우저를 띄워 OAuth 동의를 받는다(최초 연결 시). 성공하면 Refresh Token이 DataStore에 저장된다.
    /// </summary>
    public async Task<UserCredential> AuthorizeAsync(CancellationToken cancellationToken = default) =>
        await GoogleWebAuthorizationBroker.AuthorizeAsync(
            Secrets, Scopes, UserKey, cancellationToken, dataStore);

    /// <summary>
    /// 저장된 토큰만으로 자격 증명을 복원한다. 토큰이 없으면 null (브라우저를 절대 띄우지 않음 —
    /// 백그라운드 폴링 동기화에서 사용).
    /// </summary>
    public async Task<UserCredential?> TryRestoreCredentialAsync()
    {
        var token = await dataStore.GetAsync<TokenResponse>(UserKey);
        if (token?.RefreshToken is null)
            return null;

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = Secrets,
            Scopes = Scopes,
            DataStore = dataStore,
        });

        return new UserCredential(flow, UserKey, token);
    }

    /// <summary>연동 가능한 캘린더 목록. 첫 항목이 기본(primary) 캘린더가 되도록 정렬한다.</summary>
    public async Task<IReadOnlyList<GoogleCalendarInfo>> GetCalendarsAsync(
        UserCredential credential, CancellationToken cancellationToken = default)
    {
        using var service = CreateService(credential);

        var results = new List<GoogleCalendarInfo>();
        string? pageToken = null;
        do
        {
            var request = service.CalendarList.List();
            request.PageToken = pageToken;
            var response = await request.ExecuteAsync(cancellationToken);

            foreach (var entry in response.Items ?? [])
            {
                if (entry.Id is null)
                    continue;

                results.Add(new GoogleCalendarInfo(
                    entry.Id,
                    entry.Summary ?? entry.Id,
                    entry.Primary == true));
            }

            pageToken = response.NextPageToken;
        }
        while (pageToken is not null);

        return [.. results.OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Summary, StringComparer.CurrentCulture)];
    }

    /// <summary>연결된 계정의 이메일 주소. 기본 캘린더의 ID가 곧 계정 이메일이다.</summary>
    public async Task<string?> GetAccountEmailAsync(
        UserCredential credential, CancellationToken cancellationToken = default)
    {
        using var service = CreateService(credential);
        var primary = await service.CalendarList.Get("primary").ExecuteAsync(cancellationToken);
        return primary?.Id;
    }

    /// <summary>지정한 캘린더들에서 [from, to) 구간과 겹치는 이벤트를 모두 가져온다.</summary>
    public async Task<IReadOnlyList<GoogleEvent>> GetEventsAsync(
        UserCredential credential,
        IEnumerable<string> calendarIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        using var service = CreateService(credential);

        var results = new List<GoogleEvent>();
        foreach (var calendarId in calendarIds)
        {
            string? pageToken = null;
            do
            {
                var request = service.Events.List(calendarId);
                request.TimeMinDateTimeOffset = new DateTimeOffset(from);
                request.TimeMaxDateTimeOffset = new DateTimeOffset(to);
                request.SingleEvents = true; // 반복 일정을 개별 발생으로 펼쳐서 받는다
                request.ShowDeleted = false;
                request.MaxResults = 2500;
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
                request.PageToken = pageToken;

                var response = await request.ExecuteAsync(cancellationToken);

                foreach (var item in response.Items ?? [])
                {
                    var mapped = MapEvent(item, calendarId);
                    if (mapped is not null)
                        results.Add(mapped);
                }

                pageToken = response.NextPageToken;
            }
            while (pageToken is not null);
        }

        return results;
    }

    /// <summary>구글 서버의 토큰을 폐기하고 로컬 저장분도 지운다.</summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var credential = await TryRestoreCredentialAsync();
        if (credential is not null)
        {
            try
            {
                await credential.RevokeTokenAsync(cancellationToken);
            }
            catch (TokenResponseException)
            {
                // 이미 폐기됐거나 만료된 토큰 — 로컬 정리만 하면 된다
            }
        }

        await dataStore.ClearAsync();
    }

    private static CalendarService CreateService(UserCredential credential) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

    /// <summary>구글 이벤트를 로컬 캐시 모델로 변환한다. 시간 정보를 못 읽으면 null.</summary>
    private static GoogleEvent? MapEvent(Event item, string calendarId)
    {
        if (item.Id is null || item.Start is null || item.End is null)
            return null;

        var title = string.IsNullOrWhiteSpace(item.Summary) ? "(제목 없음)" : item.Summary;

        // 종일 일정은 Date("yyyy-MM-dd")로 온다. 구글의 end.date는 배타적이므로 하루 빼서 "마지막 날"로 맞춘다
        // (로컬 Schedule과 동일하게 EndAt을 포함 기준으로 다루기 위함).
        if (item.Start.Date is not null && item.End.Date is not null)
        {
            if (!TryParseDate(item.Start.Date, out var startDate) || !TryParseDate(item.End.Date, out var endDateExclusive))
                return null;

            var lastDay = endDateExclusive.AddDays(-1);
            if (lastDay < startDate)
                lastDay = startDate;

            return new GoogleEvent
            {
                Id = item.Id,
                CalendarId = calendarId,
                Title = title,
                StartAt = startDate,
                EndAt = lastDay,
                IsAllDay = true,
            };
        }

        var start = item.Start.DateTimeDateTimeOffset;
        var end = item.End.DateTimeDateTimeOffset;
        if (start is null || end is null)
            return null;

        return new GoogleEvent
        {
            Id = item.Id,
            CalendarId = calendarId,
            Title = title,
            StartAt = start.Value.LocalDateTime,
            EndAt = end.Value.LocalDateTime,
            IsAllDay = false,
        };
    }

    private static bool TryParseDate(string value, out DateTime date) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
