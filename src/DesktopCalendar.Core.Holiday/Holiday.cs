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
    /// <summary>공공데이터포털 API에서 받아온 데이터 (가장 정확 — 임시공휴일까지 반영).</summary>
    Api,

    /// <summary>사용자가 직접 추가한 공휴일.</summary>
    Manual,

    /// <summary>API 키 없이도 달력이 비어 보이지 않도록 앱이 직접 계산한 공휴일 (<see cref="KoreanHolidayCalculator"/>).</summary>
    Builtin,
}

public sealed class Holiday
{
    public required DateOnly Date { get; set; }
    public required string Name { get; set; }
    public HolidayKind Kind { get; set; }
    public HolidaySource Source { get; set; }
}
