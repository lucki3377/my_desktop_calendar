using DesktopCalendar.Core.Calendar;
using Xunit;

namespace DesktopCalendar.Core.Tests;

public class DDayCalculatorTests
{
    [Fact]
    public void OneTime_FutureDate_ReturnsPositiveDaysRemaining()
    {
        var today = new DateOnly(2026, 7, 31);
        var dday = new DDay { Title = "발표", TargetDate = new DateOnly(2026, 8, 10), IsRecurringYearly = false };

        var result = DDayCalculator.ComputeDaysRemaining(dday, today);

        Assert.Equal(10, result);
        Assert.Equal("D-10", DDayCalculator.Format(result));
    }

    [Fact]
    public void OneTime_Today_ReturnsDDay()
    {
        var today = new DateOnly(2026, 7, 31);
        var dday = new DDay { Title = "오늘", TargetDate = today, IsRecurringYearly = false };

        var result = DDayCalculator.ComputeDaysRemaining(dday, today);

        Assert.Equal(0, result);
        Assert.Equal("D-DAY", DDayCalculator.Format(result));
    }

    [Fact]
    public void OneTime_PastDate_ReturnsNegativeDaysRemaining()
    {
        var today = new DateOnly(2026, 7, 31);
        var dday = new DDay { Title = "지난 일정", TargetDate = new DateOnly(2026, 7, 20), IsRecurringYearly = false };

        var result = DDayCalculator.ComputeDaysRemaining(dday, today);

        Assert.Equal(-11, result);
        Assert.Equal("D+11", DDayCalculator.Format(result));
    }

    [Fact]
    public void Recurring_UpcomingThisYear_CountsDownToThisYear()
    {
        var today = new DateOnly(2026, 7, 31);
        var birthday = new DDay { Title = "생일", TargetDate = new DateOnly(1990, 8, 10), IsRecurringYearly = true };

        var result = DDayCalculator.ComputeDaysRemaining(birthday, today);

        Assert.Equal(10, result);
    }

    [Fact]
    public void Recurring_AlreadyPassedThisYear_RollsOverToNextYear()
    {
        var today = new DateOnly(2026, 7, 31);
        var birthday = new DDay { Title = "생일", TargetDate = new DateOnly(1990, 1, 15), IsRecurringYearly = true };

        var result = DDayCalculator.ComputeDaysRemaining(birthday, today);

        // 2027-01-15까지 남은 일수
        var expectedTarget = new DateOnly(2027, 1, 15);
        var expected = expectedTarget.DayNumber - today.DayNumber;

        Assert.Equal(expected, result);
        Assert.True(result > 0);
    }

    [Fact]
    public void Recurring_TargetIsTodayExactly_ReturnsDDay()
    {
        var today = new DateOnly(2026, 7, 31);
        var anniversary = new DDay { Title = "기념일", TargetDate = new DateOnly(2000, 7, 31), IsRecurringYearly = true };

        var result = DDayCalculator.ComputeDaysRemaining(anniversary, today);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Recurring_Feb29Birthday_NonLeapYear_ClampsToFeb28()
    {
        // 2026년은 윤년이 아님
        var today = new DateOnly(2026, 2, 1);
        var birthday = new DDay { Title = "윤년생일", TargetDate = new DateOnly(1992, 2, 29), IsRecurringYearly = true };

        var result = DDayCalculator.ComputeDaysRemaining(birthday, today);

        var expectedTarget = new DateOnly(2026, 2, 28);
        Assert.Equal(expectedTarget.DayNumber - today.DayNumber, result);
    }

    [Fact]
    public void ComputeTargetFromBase_PositiveOffset_AddsDays()
    {
        var baseDate = new DateOnly(2026, 9, 1);

        var target = DDayCalculator.ComputeTargetFromBase(baseDate, 100);

        Assert.Equal(new DateOnly(2026, 12, 10), target);
    }

    [Fact]
    public void ComputeTargetFromBase_CrossesLeapDay()
    {
        // 2028년은 윤년이라 2/29가 포함된다.
        var baseDate = new DateOnly(2028, 2, 27);

        var target = DDayCalculator.ComputeTargetFromBase(baseDate, 3);

        Assert.Equal(new DateOnly(2028, 3, 1), target);
    }

    [Fact]
    public void ComputeTargetFromBase_ZeroOffset_ReturnsBaseDate()
    {
        var baseDate = new DateOnly(2026, 9, 1);

        Assert.Equal(baseDate, DDayCalculator.ComputeTargetFromBase(baseDate, 0));
    }

    [Fact]
    public void ComputeTargetFromBase_NegativeOffset_GoesBackward()
    {
        var baseDate = new DateOnly(2026, 3, 1);

        var target = DDayCalculator.ComputeTargetFromBase(baseDate, -1);

        // 2026년은 평년이므로 2/28
        Assert.Equal(new DateOnly(2026, 2, 28), target);
    }

    [Fact]
    public void ComputeTargetFromBase_OutOfRange_Throws()
    {
        var baseDate = new DateOnly(9999, 12, 31);

        Assert.Throws<ArgumentOutOfRangeException>(() => DDayCalculator.ComputeTargetFromBase(baseDate, 1));
        Assert.False(DDayCalculator.TryComputeTargetFromBase(baseDate, 1, out _));
        Assert.False(DDayCalculator.TryComputeTargetFromBase(new DateOnly(1, 1, 1), -1, out _));
    }

    [Fact]
    public void OffsetBased_DDay_CountsDownToComputedTarget()
    {
        var today = new DateOnly(2026, 9, 1);
        var baseDate = new DateOnly(2026, 9, 1);
        var dday = new DDay
        {
            Title = "100일",
            TargetDate = DDayCalculator.ComputeTargetFromBase(baseDate, 100),
            BaseDate = baseDate,
            OffsetDays = 100,
        };

        Assert.True(dday.IsOffsetBased);
        Assert.Equal(100, DDayCalculator.ComputeDaysRemaining(dday, today));
        Assert.Equal("D-100", DDayCalculator.Format(DDayCalculator.ComputeDaysRemaining(dday, today)));
    }

    [Fact]
    public void DirectDate_DDay_IsNotOffsetBased()
    {
        var dday = new DDay { Title = "발표", TargetDate = new DateOnly(2026, 8, 10) };

        Assert.False(dday.IsOffsetBased);
    }
}
