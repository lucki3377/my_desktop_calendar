using System.Globalization;
using System.Windows.Media;
using DesktopCalendar.Core.Storage;

namespace DesktopCalendar.App;

/// <summary>
/// 위젯의 바탕 색상/불투명도와, 그 위에서 읽히는 글자 색 묶음 (DESIGN.md 4.7).
/// 바탕이 밝으면 글자를 어둡게 자동 전환하므로 흰 계열 배경을 골라도 글씨가 보인다.
/// 설정 대화상자의 미리보기와 실제 위젯이 같은 값을 쓰도록 이 클래스를 공유한다.
/// </summary>
public sealed class WidgetTheme
{
    public static readonly Color DefaultPanelColor = Color.FromRgb(0x20, 0x20, 0x20);
    public const double DefaultOpacity = 0.70;

    /// <summary>이 밝기를 넘으면 글자를 어두운 색으로 바꾼다.</summary>
    private const double DarkTextLuminanceThreshold = 0.6;

    public Color PanelColor { get; }

    /// <summary>0.0(완전 투명) ~ 1.0(불투명).</summary>
    public double Opacity { get; }

    public bool UsesDarkText { get; }

    public Brush PanelBrush { get; }

    /// <summary>월/요일/날짜 숫자 등 기본 글자색.</summary>
    public Brush PrimaryText { get; }

    /// <summary>하단 안내문, "+N개" 같은 보조 글자색.</summary>
    public Brush MutedText { get; }

    /// <summary>표시 중인 달이 아닌 날짜의 숫자.</summary>
    public Brush FadedText { get; }

    public Brush SundayText { get; }
    public Brush SaturdayText { get; }
    public Brush HolidayText { get; }

    public WidgetTheme(Color panelColor, double opacity)
    {
        PanelColor = panelColor;
        Opacity = Math.Clamp(opacity, 0.1, 1.0);
        UsesDarkText = Luminance(panelColor) > DarkTextLuminanceThreshold;

        PanelBrush = Freeze(new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(Opacity * 255), panelColor.R, panelColor.G, panelColor.B)));

        if (UsesDarkText)
        {
            PrimaryText = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)));
            MutedText = Freeze(new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)));
            FadedText = Freeze(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)));
            SundayText = Freeze(new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22)));   // Firebrick
            SaturdayText = Freeze(new SolidColorBrush(Color.FromRgb(0x25, 0x4E, 0xA6)));
            HolidayText = SundayText;
        }
        else
        {
            PrimaryText = Brushes.White;
            MutedText = Freeze(new SolidColorBrush(Color.FromArgb(180, 220, 220, 220)));
            FadedText = Freeze(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)));
            SundayText = Brushes.IndianRed;
            SaturdayText = Brushes.CornflowerBlue;
            HolidayText = Brushes.IndianRed;
        }
    }

    public static WidgetTheme Load(SettingsStore settings)
    {
        var hex = settings.GetString("Widget.PanelColor");
        var opacity = settings.GetDouble("Widget.PanelOpacity", DefaultOpacity);
        return new WidgetTheme(ParseColor(hex) ?? DefaultPanelColor, opacity);
    }

    public void Save(SettingsStore settings)
    {
        settings.SetString("Widget.PanelColor", ToHex(PanelColor));
        settings.SetDouble("Widget.PanelOpacity", Opacity);
    }

    public static string ToHex(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Color? ParseColor(string? hex)
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

    /// <summary>사람 눈이 느끼는 밝기(0~1). 초록에 가중치가 큰 표준 가중합을 쓴다.</summary>
    private static double Luminance(Color color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    /// <summary>설정 창에 보여줄 기본 제공 색상.</summary>
    public static IReadOnlyList<(string Name, Color Color)> Presets { get; } =
    [
        ("검정", Color.FromRgb(0x20, 0x20, 0x20)),
        ("진회색", Color.FromRgb(0x3A, 0x3F, 0x44)),
        ("남색", Color.FromRgb(0x1E, 0x30, 0x54)),
        ("진초록", Color.FromRgb(0x1E, 0x3D, 0x33)),
        ("자주", Color.FromRgb(0x3B, 0x22, 0x3F)),
        ("흰색", Color.FromRgb(0xF5, 0xF5, 0xF5)),
        ("크림", Color.FromRgb(0xF0, 0xE6, 0xD2)),
        ("연회색", Color.FromRgb(0xD8, 0xDC, 0xE0)),
    ];

    public override string ToString() =>
        $"{ToHex(PanelColor)} @ {Opacity.ToString("P0", CultureInfo.CurrentCulture)}";
}
