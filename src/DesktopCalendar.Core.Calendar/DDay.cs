namespace DesktopCalendar.Core.Calendar;

public sealed class DDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }

    /// <summary>실제로 카운트할 날짜. 기준일 방식으로 만든 항목도 이 값이 계산의 기준이다.</summary>
    public required DateOnly TargetDate { get; set; }

    public bool IsRecurringYearly { get; set; }

    /// <summary>
    /// "기준일로부터 N일 후" 방식으로 만든 항목의 기준일. 날짜를 직접 지정한 항목은 null.
    /// <see cref="OffsetDays"/>와 항상 함께 설정된다.
    /// </summary>
    public DateOnly? BaseDate { get; set; }

    /// <summary>기준일에 더한 일수(음수면 기준일 이전). 날짜를 직접 지정한 항목은 null.</summary>
    public int? OffsetDays { get; set; }

    /// <summary>기준일 방식으로 만들어진 항목인지 여부.</summary>
    public bool IsOffsetBased => BaseDate.HasValue && OffsetDays.HasValue;
}
