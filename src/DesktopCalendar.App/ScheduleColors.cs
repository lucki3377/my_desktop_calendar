using System.Windows.Media;

namespace DesktopCalendar.App;

/// <summary>
/// 일정 칩에 쓸 수 있는 색상 팔레트 (DESIGN.md 4.3의 색상 태그).
/// 구글 일정을 나타내는 초록 계열은 구분이 흐려지지 않도록 팔레트에서 뺐다.
/// </summary>
public static class ScheduleColors
{
    /// <summary>색을 따로 고르지 않은 일정에 쓰는 기본 칩 색.</summary>
    public static readonly Color Default = Color.FromArgb(210, 70, 130, 200);

    /// <summary>구글에서 가져온 일정의 칩 색.</summary>
    public static readonly Color Google = Color.FromArgb(210, 55, 150, 105);

    /// <summary>선택지 목록. Hex가 null이면 "기본색"(일정에 색을 저장하지 않음)을 뜻한다.</summary>
    public static IReadOnlyList<(string Name, string? Hex)> Palette { get; } =
    [
        ("기본", null),
        ("빨강", "#C0504D"),
        ("주황", "#DD8A3C"),
        ("황토", "#C9A227"),
        ("보라", "#8A5FBF"),
        ("분홍", "#C2568F"),
        ("청록", "#3E8E9E"),
        ("회색", "#6E7377"),
    ];

    /// <summary>저장된 색 문자열을 브러시로 바꾼다. 값이 없거나 형식이 깨졌으면 기본색.</summary>
    public static SolidColorBrush ToBrush(string? hex, bool isGoogle = false)
    {
        var parsed = Parse(hex);
        if (parsed is not null)
            return new SolidColorBrush(parsed.Value);

        return new SolidColorBrush(isGoogle ? Google : Default);
    }

    public static Color? Parse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;

        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
