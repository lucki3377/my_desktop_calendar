using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.Core.Tests;

public class KoreanLunarDateTests
{
    [Theory]
    // 설날은 정의상 음력 1월 1일이다 (공휴일 계산기 쪽에서 이미 날짜를 검증해 둔 해들)
    [InlineData(2024, 2, 10, "1.1")]
    [InlineData(2025, 1, 29, "1.1")]
    [InlineData(2026, 2, 17, "1.1")]
    // 추석은 음력 8월 15일
    [InlineData(2024, 9, 17, "8.15")]
    [InlineData(2025, 10, 6, "8.15")]
    [InlineData(2026, 9, 25, "8.15")]
    // 부처님오신날은 음력 4월 8일
    [InlineData(2026, 5, 24, "4.8")]
    public void 알려진_음력_명절_날짜와_일치한다(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, KoreanLunarDate.Format(new DateOnly(year, month, day)));
    }

    [Fact]
    public void 설날_하루_전은_전달의_마지막_날이다()
    {
        // 2026년 설날은 2/17 → 하루 전은 음력 12월 (그믐)
        var parts = KoreanLunarDate.Convert(new DateOnly(2026, 2, 16));

        Assert.NotNull(parts);
        Assert.Equal(12, parts!.Value.Month);
    }

    [Fact]
    public void 윤달은_윤_표기가_붙는다()
    {
        // 2025년은 윤6월이 있는 해다.
        var leapDays = Enumerable.Range(0, 365)
            .Select(offset => new DateOnly(2025, 1, 1).AddDays(offset))
            .Select(KoreanLunarDate.Format)
            .Where(label => label is not null && label.StartsWith('윤'))
            .ToList();

        Assert.NotEmpty(leapDays);
        Assert.All(leapDays, label => Assert.StartsWith("윤6.", label));
    }

    [Fact]
    public void 지원하지_않는_먼_미래는_null을_돌려준다()
    {
        Assert.Null(KoreanLunarDate.Format(new DateOnly(2100, 1, 1)));
    }

    [Fact]
    public void 한_해_모든_날짜를_변환해도_예외가_없다()
    {
        for (var date = new DateOnly(2026, 1, 1); date.Year == 2026; date = date.AddDays(1))
            Assert.NotNull(KoreanLunarDate.Format(date));
    }
}
