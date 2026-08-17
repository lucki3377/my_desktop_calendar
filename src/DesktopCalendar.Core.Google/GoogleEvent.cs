namespace DesktopCalendar.Core.Google;

/// <summary>
/// 구글 캘린더에서 읽어와 로컬에 캐시한 이벤트 (DESIGN.md 5 GoogleEventCache).
/// 읽기 전용 — 앱에서 수정/삭제하지 않는다(단방향 동기화).
/// </summary>
public sealed class GoogleEvent
{
    public required string Id { get; set; }
    public required string CalendarId { get; set; }
    public required string Title { get; set; }

    /// <summary>종일 일정이면 시작일 00:00.</summary>
    public required DateTime StartAt { get; set; }

    /// <summary>종일 일정이면 "마지막 날" 00:00 (구글 API의 배타적 end.date에서 하루 뺀 값).</summary>
    public required DateTime EndAt { get; set; }

    public bool IsAllDay { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.Now;
}

/// <summary>설정 화면의 캘린더 선택 목록에 쓰는 캘린더 요약 정보.</summary>
public sealed record GoogleCalendarInfo(string Id, string Summary, bool IsPrimary);
