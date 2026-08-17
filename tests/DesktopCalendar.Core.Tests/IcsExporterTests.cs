using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.Core.Tests;

public class IcsExporterTests
{
    private static readonly DateTime Stamp = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    private static Schedule Make(string title = "회의") => new()
    {
        Title = title,
        StartAt = new DateTime(2026, 8, 17, 9, 0, 0),
        EndAt = new DateTime(2026, 8, 17, 10, 0, 0),
    };

    private static string[] Lines(string ics) =>
        ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void 달력_봉투로_감싼다()
    {
        var lines = Lines(IcsExporter.Export([Make()], Stamp));

        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.Equal("END:VCALENDAR", lines[^1]);
        Assert.Contains("VERSION:2.0", lines);
    }

    [Fact]
    public void 시간이_있는_일정은_지역시간_형식으로_나간다()
    {
        var lines = Lines(IcsExporter.Export([Make()], Stamp));

        Assert.Contains("DTSTART:20260817T090000", lines);
        Assert.Contains("DTEND:20260817T100000", lines);
    }

    [Fact]
    public void 종일_일정의_종료일은_하루_뒤로_나간다()
    {
        var schedule = Make();
        schedule.IsAllDay = true;
        schedule.StartAt = new DateTime(2026, 8, 17);
        schedule.EndAt = new DateTime(2026, 8, 19);

        var lines = Lines(IcsExporter.Export([schedule], Stamp));

        Assert.Contains("DTSTART;VALUE=DATE:20260817", lines);
        Assert.Contains("DTEND;VALUE=DATE:20260820", lines); // 배타적 종료
    }

    [Theory]
    [InlineData(RecurrenceType.Daily, "RRULE:FREQ=DAILY")]
    [InlineData(RecurrenceType.Weekly, "RRULE:FREQ=WEEKLY")]
    [InlineData(RecurrenceType.Monthly, "RRULE:FREQ=MONTHLY")]
    [InlineData(RecurrenceType.Yearly, "RRULE:FREQ=YEARLY")]
    public void 반복은_RRULE_한_줄로_나간다(RecurrenceType type, string expected)
    {
        var schedule = Make();
        schedule.Recurrence = type;

        Assert.Contains(expected, Lines(IcsExporter.Export([schedule], Stamp)));
    }

    [Fact]
    public void 반복_종료일은_UNTIL로_나간다()
    {
        var schedule = Make();
        schedule.Recurrence = RecurrenceType.Weekly;
        schedule.RecurrenceUntil = new DateOnly(2026, 12, 31);

        Assert.Contains("RRULE:FREQ=WEEKLY;UNTIL=20261231T235959", Lines(IcsExporter.Export([schedule], Stamp)));
    }

    [Fact]
    public void 제외_날짜는_EXDATE로_나간다()
    {
        var schedule = Make();
        schedule.Recurrence = RecurrenceType.Weekly;
        schedule.RecurrenceExceptions = [new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 31)];

        // 시간이 있는 일정이므로 시작 시각(09:00)을 붙여서 나가야 회차가 매칭된다
        Assert.Contains("EXDATE:20260824T090000,20260831T090000", Lines(IcsExporter.Export([schedule], Stamp)));
    }

    [Fact]
    public void 음력_반복은_RRULE_대신_RDATE로_나간다()
    {
        var schedule = Make();
        schedule.StartAt = new DateTime(2026, 2, 17, 9, 0, 0); // 음력 1월 1일
        schedule.EndAt = schedule.StartAt.AddHours(1);
        schedule.Recurrence = RecurrenceType.LunarYearly;

        var lines = Lines(IcsExporter.Export([schedule], Stamp));

        Assert.DoesNotContain(lines, l => l.StartsWith("RRULE"));

        // 접힌 줄을 다시 이어 붙여서 확인한다
        var joined = string.Concat(lines.Select(l => l.StartsWith(' ') ? l[1..] : "\n" + l));
        Assert.Contains("RDATE:", joined);
        Assert.Contains("20270207T090000", joined); // 2027년 설날
    }

    [Fact]
    public void 반복이_아니면_RRULE이_없다()
    {
        Assert.DoesNotContain(Lines(IcsExporter.Export([Make()], Stamp)), l => l.StartsWith("RRULE"));
    }

    [Fact]
    public void 쉼표와_세미콜론과_줄바꿈을_이스케이프한다()
    {
        var schedule = Make("점심, 회의; 급함");
        schedule.Description = "첫 줄\n둘째 줄";

        var lines = Lines(IcsExporter.Export([schedule], Stamp));

        Assert.Contains(@"SUMMARY:점심\, 회의\; 급함", lines);
        Assert.Contains(@"DESCRIPTION:첫 줄\n둘째 줄", lines);
    }

    [Fact]
    public void 긴_줄은_접어서_내보낸다()
    {
        var schedule = Make(new string('가', 200));

        var lines = Lines(IcsExporter.Export([schedule], Stamp));

        Assert.All(lines, l => Assert.True(l.Length <= 74, $"너무 긴 줄: {l.Length}자"));
        // 접힌 줄은 공백으로 시작해야 이어붙일 수 있다
        Assert.Contains(lines, l => l.StartsWith(' '));
    }

    [Fact]
    public void 일정마다_고유한_UID가_붙는다()
    {
        var a = Make("A");
        var b = Make("B");

        var lines = Lines(IcsExporter.Export([a, b], Stamp));
        var uids = lines.Where(l => l.StartsWith("UID:")).ToList();

        Assert.Equal(2, uids.Count);
        Assert.Equal(2, uids.Distinct().Count());
    }

    [Fact]
    public void 일정이_없어도_유효한_달력을_만든다()
    {
        var lines = Lines(IcsExporter.Export([], Stamp));

        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.Equal("END:VCALENDAR", lines[^1]);
        Assert.DoesNotContain("BEGIN:VEVENT", lines);
    }
}
