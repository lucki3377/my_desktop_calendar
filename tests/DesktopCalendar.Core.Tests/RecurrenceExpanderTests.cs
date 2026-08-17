using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.Core.Tests;

public class RecurrenceExpanderTests
{
    private static Schedule Make(
        DateTime startAt,
        RecurrenceType recurrence = RecurrenceType.None,
        TimeSpan? duration = null,
        DateOnly? until = null,
        IReadOnlyList<DateOnly>? exceptions = null) => new()
    {
        Title = "테스트",
        StartAt = startAt,
        EndAt = startAt + (duration ?? TimeSpan.FromHours(1)),
        Recurrence = recurrence,
        RecurrenceUntil = until,
        RecurrenceExceptions = exceptions ?? [],
    };

    private static (DateTime Start, DateTime End) Month(int year, int month) =>
        (new DateTime(year, month, 1), new DateTime(year, month, 1).AddMonths(1));

    [Fact]
    public void 반복이_없으면_구간에_겹칠_때만_한_번_나온다()
    {
        var schedule = Make(new DateTime(2026, 8, 20, 9, 0, 0));
        var (start, end) = Month(2026, 8);

        Assert.Single(RecurrenceExpander.Expand(schedule, start, end));
        Assert.Empty(RecurrenceExpander.Expand(schedule, Month(2026, 9).Start, Month(2026, 9).End));
    }

    [Fact]
    public void 매주_반복은_해당_요일마다_나온다()
    {
        // 2026-08-03은 월요일. 8월의 월요일은 3, 10, 17, 24, 31 다섯 번.
        var schedule = Make(new DateTime(2026, 8, 3, 9, 0, 0), RecurrenceType.Weekly);
        var (start, end) = Month(2026, 8);

        var occurrences = RecurrenceExpander.Expand(schedule, start, end);

        Assert.Equal(5, occurrences.Count);
        Assert.All(occurrences, o => Assert.Equal(DayOfWeek.Monday, o.StartAt.DayOfWeek));
        Assert.Equal(new DateTime(2026, 8, 31, 9, 0, 0), occurrences[^1].StartAt);
    }

    [Fact]
    public void 시작보다_한참_뒤의_달도_올바르게_펼쳐진다()
    {
        var schedule = Make(new DateTime(2020, 1, 1, 9, 0, 0), RecurrenceType.Daily);
        var (start, end) = Month(2026, 8);

        var occurrences = RecurrenceExpander.Expand(schedule, start, end);

        Assert.Equal(31, occurrences.Count);
        Assert.Equal(new DateTime(2026, 8, 1, 9, 0, 0), occurrences[0].StartAt);
    }

    [Fact]
    public void 매달_31일_반복은_31일이_없는_달을_건너뛴다()
    {
        var schedule = Make(new DateTime(2026, 1, 31, 9, 0, 0), RecurrenceType.Monthly);

        Assert.Empty(RecurrenceExpander.Expand(schedule, Month(2026, 2).Start, Month(2026, 2).End));
        Assert.Empty(RecurrenceExpander.Expand(schedule, Month(2026, 4).Start, Month(2026, 4).End));
        Assert.Single(RecurrenceExpander.Expand(schedule, Month(2026, 3).Start, Month(2026, 3).End));
    }

    [Fact]
    public void 매년_2월_29일_반복은_평년을_건너뛴다()
    {
        var schedule = Make(new DateTime(2024, 2, 29, 9, 0, 0), RecurrenceType.Yearly);

        Assert.Empty(RecurrenceExpander.Expand(schedule, Month(2026, 2).Start, Month(2026, 2).End));
        Assert.Single(RecurrenceExpander.Expand(schedule, Month(2028, 2).Start, Month(2028, 2).End));
    }

    [Fact]
    public void 종료일_이후에는_열리지_않는다()
    {
        var schedule = Make(
            new DateTime(2026, 8, 3, 9, 0, 0),
            RecurrenceType.Weekly,
            until: new DateOnly(2026, 8, 17));

        var occurrences = RecurrenceExpander.Expand(schedule, Month(2026, 8).Start, Month(2026, 8).End);

        Assert.Equal(3, occurrences.Count); // 3, 10, 17
        Assert.Equal(new DateTime(2026, 8, 17, 9, 0, 0), occurrences[^1].StartAt);
    }

    [Fact]
    public void 제외한_날짜는_건너뛴다()
    {
        var schedule = Make(
            new DateTime(2026, 8, 3, 9, 0, 0),
            RecurrenceType.Weekly,
            exceptions: [new DateOnly(2026, 8, 17)]);

        var occurrences = RecurrenceExpander.Expand(schedule, Month(2026, 8).Start, Month(2026, 8).End);

        Assert.Equal(4, occurrences.Count);
        Assert.DoesNotContain(occurrences, o => o.StartDate == new DateOnly(2026, 8, 17));
    }

    [Fact]
    public void 다일_반복은_구간_시작에_걸쳐_있어도_잡힌다()
    {
        // 매달 30일에 시작해 3일간 이어지는 일정 → 7/30~8/1 회차는 8월 구간에도 걸린다.
        var schedule = Make(
            new DateTime(2026, 7, 30, 9, 0, 0),
            RecurrenceType.Monthly,
            duration: TimeSpan.FromDays(2));

        var occurrences = RecurrenceExpander.Expand(schedule, Month(2026, 8).Start, Month(2026, 8).End);

        Assert.Equal(2, occurrences.Count); // 7/30 시작분(8/1까지) + 8/30 시작분
        Assert.Equal(new DateTime(2026, 7, 30, 9, 0, 0), occurrences[0].StartAt);
    }

    [Fact]
    public void 회차의_길이는_원본과_같다()
    {
        var schedule = Make(new DateTime(2026, 8, 3, 9, 0, 0), RecurrenceType.Weekly, duration: TimeSpan.FromHours(3));

        var occurrences = RecurrenceExpander.Expand(schedule, Month(2026, 8).Start, Month(2026, 8).End);

        Assert.All(occurrences, o => Assert.Equal(TimeSpan.FromHours(3), o.EndAt - o.StartAt));
    }

    [Fact]
    public void 여러_일정을_한꺼번에_펼치면_시작_시각_순이다()
    {
        var weekly = Make(new DateTime(2026, 8, 3, 15, 0, 0), RecurrenceType.Weekly);
        var single = Make(new DateTime(2026, 8, 4, 9, 0, 0));

        var occurrences = RecurrenceExpander.ExpandAll([weekly, single], Month(2026, 8).Start, Month(2026, 8).End);

        Assert.Equal(new DateTime(2026, 8, 3, 15, 0, 0), occurrences[0].StartAt);
        Assert.Equal(new DateTime(2026, 8, 4, 9, 0, 0), occurrences[1].StartAt);
    }

    [Fact]
    public void 시작_전의_달에는_열리지_않는다()
    {
        var schedule = Make(new DateTime(2026, 8, 3, 9, 0, 0), RecurrenceType.Weekly);

        Assert.Empty(RecurrenceExpander.Expand(schedule, Month(2026, 7).Start, Month(2026, 7).End));
    }
}
