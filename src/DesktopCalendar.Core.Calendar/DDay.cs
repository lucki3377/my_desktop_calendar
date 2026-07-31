namespace DesktopCalendar.Core.Calendar;

public sealed class DDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required DateOnly TargetDate { get; set; }
    public bool IsRecurringYearly { get; set; }
}
