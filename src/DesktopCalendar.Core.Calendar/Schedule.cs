namespace DesktopCalendar.Core.Calendar;

/// <summary>일정 반복 주기 (DESIGN.md 4.11).</summary>
public enum RecurrenceType
{
    None,
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

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

    /// <summary>반복 주기. <see cref="RecurrenceType.None"/>이면 <see cref="StartAt"/> 한 번만 열린다.</summary>
    public RecurrenceType Recurrence { get; set; } = RecurrenceType.None;

    /// <summary>반복을 끝낼 날짜(포함). null이면 무기한 반복.</summary>
    public DateOnly? RecurrenceUntil { get; set; }

    /// <summary>반복 중 건너뛸 날짜들 (개별 회차를 삭제하면 여기에 쌓인다).</summary>
    public IReadOnlyList<DateOnly> RecurrenceExceptions { get; set; } = [];

    public bool IsRecurring => Recurrence != RecurrenceType.None;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
