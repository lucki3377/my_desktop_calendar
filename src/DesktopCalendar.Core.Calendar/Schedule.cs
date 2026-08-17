namespace DesktopCalendar.Core.Calendar;

public sealed class Schedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required DateTime StartAt { get; set; }
    public required DateTime EndAt { get; set; }
    public bool IsAllDay { get; set; }
    public string? Color { get; set; }

    /// <summary>
    /// 시작 몇 분 전에 알릴지. null이면 알리지 않는다.
    /// 0이면 시작 시각에 알린다(종일 일정은 그 날 0시 기준).
    /// </summary>
    public int? ReminderMinutesBefore { get; set; }

    /// <summary>알림을 띄워야 하는 시각. 알림이 꺼져 있으면 null.</summary>
    public DateTime? ReminderAt =>
        ReminderMinutesBefore is null ? null : StartAt.AddMinutes(-ReminderMinutesBefore.Value);
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
