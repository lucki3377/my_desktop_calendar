namespace DesktopCalendar.Core.Holiday;

public enum HolidayKind
{
    PublicHoliday,
    SubstituteHoliday,
    TemporaryHoliday,
    Manual,
}

public enum HolidaySource
{
    Api,
    Manual,
}

public sealed class Holiday
{
    public required DateOnly Date { get; set; }
    public required string Name { get; set; }
    public HolidayKind Kind { get; set; }
    public HolidaySource Source { get; set; }
}
