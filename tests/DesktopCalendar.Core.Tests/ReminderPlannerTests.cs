using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.Core.Tests;

public class ReminderPlannerTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 14, 0, 0);

    private static ScheduleOccurrence At(DateTime startAt, int? reminderMinutes)
    {
        var schedule = new Schedule
        {
            Title = "테스트",
            StartAt = startAt,
            EndAt = startAt.AddHours(1),
            ReminderMinutesBefore = reminderMinutes,
        };

        return new ScheduleOccurrence(schedule, schedule.StartAt, schedule.EndAt);
    }

    [Fact]
    public void 알림_시각이_구간에_들어오면_선택된다()
    {
        // 14:10 시작 + 10분 전 알림 → 알림 시각 14:00
        var schedule = At(Now.AddMinutes(10), 10);

        var due = ReminderPlanner.SelectDue([schedule], Now.AddMinutes(-1), Now);

        Assert.Single(due);
    }

    [Fact]
    public void 이미_지난_구간의_알림은_다시_선택되지_않는다()
    {
        var schedule = At(Now.AddMinutes(10), 10); // 알림 시각 14:00

        // 지난번 확인 시점이 이미 14:00을 지난 경우
        var due = ReminderPlanner.SelectDue([schedule], Now, Now.AddMinutes(1));

        Assert.Empty(due);
    }

    [Fact]
    public void 아직_알림_시각이_안_됐으면_선택되지_않는다()
    {
        var schedule = At(Now.AddHours(2), 10); // 알림 시각 15:50

        var due = ReminderPlanner.SelectDue([schedule], Now.AddMinutes(-1), Now);

        Assert.Empty(due);
    }

    [Fact]
    public void 알림이_꺼진_일정은_선택되지_않는다()
    {
        var schedule = At(Now, null);

        var due = ReminderPlanner.SelectDue([schedule], Now.AddMinutes(-5), Now);

        Assert.Empty(due);
    }

    [Fact]
    public void 알림_0분은_시작_시각에_선택된다()
    {
        var schedule = At(Now, 0);

        var due = ReminderPlanner.SelectDue([schedule], Now.AddMinutes(-1), Now);

        Assert.Single(due);
    }

    [Fact]
    public void 여러_건은_시작_시각_순으로_정렬된다()
    {
        var later = At(Now.AddMinutes(30), 30);
        var sooner = At(Now.AddMinutes(10), 10);

        var due = ReminderPlanner.SelectDue([later, sooner], Now.AddMinutes(-1), Now);

        Assert.Equal(2, due.Count);
        Assert.Equal(sooner.StartAt, due[0].StartAt);
    }

    [Fact]
    public void 마지막_확인_시점이_없으면_최대_소급_구간까지만_거슬러_올라간다()
    {
        var result = ReminderPlanner.ClampLookback(null, Now, TimeSpan.FromMinutes(10));

        Assert.Equal(Now.AddMinutes(-10), result);
    }

    [Fact]
    public void 오래_꺼져_있었으면_밀린_알림을_건너뛴다()
    {
        var lastChecked = Now.AddDays(-3);

        var result = ReminderPlanner.ClampLookback(lastChecked, Now, TimeSpan.FromMinutes(10));

        Assert.Equal(Now.AddMinutes(-10), result);
    }

    [Fact]
    public void 최근에_확인했으면_그_시점부터_이어서_본다()
    {
        var lastChecked = Now.AddMinutes(-2);

        var result = ReminderPlanner.ClampLookback(lastChecked, Now, TimeSpan.FromMinutes(10));

        Assert.Equal(lastChecked, result);
    }

    [Theory]
    [InlineData(0, "지금 시작합니다.")]
    [InlineData(10, "10분 뒤에 시작합니다.")]
    [InlineData(60, "1시간 뒤에 시작합니다.")]
    [InlineData(90, "1시간 30분 뒤에 시작합니다.")]
    [InlineData(1440, "내일 시작합니다.")]
    public void 알림_문구를_사람이_읽는_형태로_만든다(int minutes, string expected)
    {
        Assert.Equal(expected, ReminderPlanner.DescribeLeadTime(At(Now, minutes)));
    }
}
