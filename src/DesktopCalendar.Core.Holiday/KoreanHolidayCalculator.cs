using System.Globalization;

namespace DesktopCalendar.Core.Holiday;

/// <summary>
/// 한국 공휴일을 앱에서 직접 계산한다 (DESIGN.md 4.2 — API 키가 없어도 달력이 비어 보이지 않도록 하는 기본값).
///
/// 근거: "관공서의 공휴일에 관한 규정". 음력 공휴일(설날·부처님오신날·추석)은 .NET의
/// <see cref="KoreanLunisolarCalendar"/>로 양력 날짜를 구한다.
///
/// 한계: 정부가 그때그때 지정하는 <b>임시공휴일</b>은 계산으로 알 수 없다 →
/// 공공데이터포털 API 키를 넣거나 사용자가 수동으로 추가해야 한다.
/// </summary>
public static class KoreanHolidayCalculator
{
    private static readonly KoreanLunisolarCalendar Lunar = new();

    /// <summary>대체공휴일 제도가 설날·추석·어린이날에 적용되기 시작한 해.</summary>
    private const int SubstituteSinceLunarAndChildren = 2014;

    /// <summary>3·1절, 광복절, 개천절, 한글날에 대체공휴일이 적용되기 시작한 해.</summary>
    private const int SubstituteSinceNationalDays = 2021;

    /// <summary>부처님오신날, 성탄절에 대체공휴일이 적용되기 시작한 해.</summary>
    private const int SubstituteSinceBuddhaAndChristmas = 2023;

    /// <summary>해당 연도의 공휴일 목록(대체공휴일 포함)을 날짜순으로 돌려준다.</summary>
    public static IReadOnlyList<Holiday> GetHolidays(int year)
    {
        // 1단계: 대체공휴일을 따지기 전의 "본래" 공휴일들을 모은다.
        var baseHolidays = new List<BaseHoliday>
        {
            new(new DateOnly(year, 1, 1), "1월 1일", SubstituteRule.None),
            new(new DateOnly(year, 3, 1), "삼일절", SubstituteRule.WeekendOnly),
            new(new DateOnly(year, 5, 5), "어린이날", SubstituteRule.WeekendOrOtherHoliday),
            new(new DateOnly(year, 6, 6), "현충일", SubstituteRule.None),
            new(new DateOnly(year, 8, 15), "광복절", SubstituteRule.WeekendOnly),
            new(new DateOnly(year, 10, 3), "개천절", SubstituteRule.WeekendOnly),
            new(new DateOnly(year, 10, 9), "한글날", SubstituteRule.WeekendOnly),
            new(new DateOnly(year, 12, 25), "기독탄신일", SubstituteRule.WeekendOnly),
        };

        AddLunarHolidays(year, baseHolidays);

        // 같은 날에 두 공휴일이 겹칠 수 있으므로(예: 2025년 어린이날 & 부처님오신날) 날짜 기준으로 정리한다.
        baseHolidays.Sort((a, b) => a.Date.CompareTo(b.Date));

        var result = new Dictionary<DateOnly, Holiday>();
        foreach (var item in baseHolidays)
        {
            if (result.TryGetValue(item.Date, out var existing))
                existing.Name = $"{existing.Name}, {item.Name}"; // 겹치면 이름을 합쳐서 보여준다
            else
                result[item.Date] = new Holiday
                {
                    Date = item.Date,
                    Name = item.Name,
                    Kind = HolidayKind.PublicHoliday,
                    Source = HolidaySource.Builtin,
                };
        }

        // 2단계: 대체공휴일을 얹는다.
        foreach (var substitute in ComputeSubstitutes(year, baseHolidays, result))
            result[substitute.Date] = substitute;

        return [.. result.Values.OrderBy(h => h.Date)];
    }

    private static void AddLunarHolidays(int year, List<BaseHoliday> holidays)
    {
        // 설날: 음력 1월 1일과 그 앞뒤 하루씩 (연휴가 전년도 12월로 넘어가는 해도 있으므로 연도 필터는 뒤에서 한다)
        var seollal = LunarToSolar(year, 1, 1);
        if (seollal is not null)
        {
            AddIfInYear(holidays, year, seollal.Value.AddDays(-1), "설날 연휴", SubstituteRule.LunarHoliday);
            AddIfInYear(holidays, year, seollal.Value, "설날", SubstituteRule.LunarHoliday);
            AddIfInYear(holidays, year, seollal.Value.AddDays(1), "설날 연휴", SubstituteRule.LunarHoliday);
        }

        // 부처님오신날: 음력 4월 8일
        var buddha = LunarToSolar(year, 4, 8);
        if (buddha is not null)
        {
            var rule = year >= SubstituteSinceBuddhaAndChristmas ? SubstituteRule.WeekendOnly : SubstituteRule.None;
            AddIfInYear(holidays, year, buddha.Value, "부처님오신날", rule);
        }

        // 추석: 음력 8월 15일과 그 앞뒤 하루씩
        var chuseok = LunarToSolar(year, 8, 15);
        if (chuseok is not null)
        {
            AddIfInYear(holidays, year, chuseok.Value.AddDays(-1), "추석 연휴", SubstituteRule.LunarHoliday);
            AddIfInYear(holidays, year, chuseok.Value, "추석", SubstituteRule.LunarHoliday);
            AddIfInYear(holidays, year, chuseok.Value.AddDays(1), "추석 연휴", SubstituteRule.LunarHoliday);
        }
    }

    private static void AddIfInYear(
        List<BaseHoliday> holidays, int year, DateOnly date, string name, SubstituteRule rule)
    {
        if (date.Year == year)
            holidays.Add(new BaseHoliday(date, name, rule));
    }

    /// <summary>
    /// 대체공휴일을 계산한다. 규칙별로 "겹침" 판정이 다르다:
    /// 설날·추석 연휴는 일요일/다른 공휴일과 겹칠 때, 어린이날은 주말/다른 공휴일과 겹칠 때,
    /// 나머지(삼일절·광복절·개천절·한글날·부처님오신날·성탄절)는 주말과 겹칠 때만.
    /// 대체일은 "그 다음 첫 번째 비공휴일"(주말도 아니고 이미 공휴일도 아닌 날)이다.
    /// </summary>
    private static List<Holiday> ComputeSubstitutes(
        int year, List<BaseHoliday> baseHolidays, Dictionary<DateOnly, Holiday> baseByDate)
    {
        var occupied = new HashSet<DateOnly>(baseByDate.Keys);
        var substitutes = new List<Holiday>();

        foreach (var item in baseHolidays)
        {
            if (!IsSubstituteEligible(year, item))
                continue;

            var isWeekend = item.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var isSunday = item.Date.DayOfWeek == DayOfWeek.Sunday;

            // 같은 날짜에 다른 공휴일이 함께 있는지 (예: 2025년 어린이날 & 부처님오신날)
            var overlapsOtherHoliday = baseHolidays.Any(other =>
                other.Date == item.Date && !ReferenceEquals(other, item) && other.Name != item.Name);

            var needsSubstitute = item.Rule switch
            {
                SubstituteRule.LunarHoliday => isSunday || overlapsOtherHoliday,
                SubstituteRule.WeekendOrOtherHoliday => isWeekend || overlapsOtherHoliday,
                SubstituteRule.WeekendOnly => isWeekend,
                _ => false,
            };

            if (!needsSubstitute)
                continue;

            var candidate = item.Date.AddDays(1);
            while (occupied.Contains(candidate)
                   || candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                candidate = candidate.AddDays(1);
            }

            if (candidate.Year != year)
                continue; // 연말 공휴일의 대체일이 다음 해로 넘어가면 그 해 계산에 맡긴다

            occupied.Add(candidate);
            substitutes.Add(new Holiday
            {
                Date = candidate,
                Name = "대체공휴일",
                Kind = HolidayKind.SubstituteHoliday,
                Source = HolidaySource.Builtin,
            });
        }

        return substitutes;
    }

    /// <summary>대체공휴일 제도가 그 해에 그 공휴일까지 확대되어 있었는지.</summary>
    private static bool IsSubstituteEligible(int year, BaseHoliday item) => item.Rule switch
    {
        SubstituteRule.LunarHoliday => year >= SubstituteSinceLunarAndChildren,
        SubstituteRule.WeekendOrOtherHoliday => year >= SubstituteSinceLunarAndChildren,
        SubstituteRule.WeekendOnly => item.Name is "부처님오신날" or "기독탄신일"
            ? year >= SubstituteSinceBuddhaAndChristmas
            : year >= SubstituteSinceNationalDays,
        _ => false,
    };

    /// <summary>음력 날짜를 양력으로 변환한다. 윤달이 앞에 끼면 월 인덱스가 하나 밀리는 것을 보정한다.</summary>
    private static DateOnly? LunarToSolar(int lunarYear, int lunarMonth, int lunarDay)
    {
        try
        {
            var leapMonth = Lunar.GetLeapMonth(lunarYear);
            var monthIndex = leapMonth > 0 && lunarMonth >= leapMonth ? lunarMonth + 1 : lunarMonth;

            return DateOnly.FromDateTime(Lunar.ToDateTime(lunarYear, monthIndex, lunarDay, 0, 0, 0, 0));
        }
        catch (ArgumentOutOfRangeException)
        {
            // KoreanLunisolarCalendar가 지원하지 않는 연도(대략 2051년 이후) — 음력 공휴일은 건너뛴다
            return null;
        }
    }

    private enum SubstituteRule
    {
        /// <summary>대체공휴일 대상 아님 (1월 1일, 현충일).</summary>
        None,

        /// <summary>토·일요일과 겹칠 때만 대체.</summary>
        WeekendOnly,

        /// <summary>토·일요일 또는 다른 공휴일과 겹칠 때 대체 (어린이날).</summary>
        WeekendOrOtherHoliday,

        /// <summary>일요일 또는 다른 공휴일과 겹칠 때 대체 (설날·추석 연휴 — 토요일은 해당 없음).</summary>
        LunarHoliday,
    }

    private sealed record BaseHoliday(DateOnly Date, string Name, SubstituteRule Rule);
}
