using System.Globalization;
using System.Windows;
using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.App;

/// <summary>
/// 음력 날짜를 골라 양력으로 환산해 주는 창 (DESIGN.md 4.14).
/// 고르는 즉시 아래에 양력 날짜를 보여줘서, 확인을 누르기 전에 맞는 날인지 알 수 있다.
/// </summary>
public partial class LunarDatePickerDialog : Window
{
    private static readonly CultureInfo Korean = new("ko-KR");

    /// <summary><see cref="KoreanLunisolarCalendar"/>가 다룰 수 있는 범위 안에서 넉넉히 잡은 연도 목록.</summary>
    private const int MinYear = 1950;
    private const int MaxYear = 2049;

    private bool _isLoading = true;

    /// <summary>확인을 눌렀을 때 고른 음력 날짜에 해당하는 양력 날짜.</summary>
    public DateOnly? SelectedSolarDate { get; private set; }

    public LunarDatePickerDialog(DateOnly initialSolarDate)
    {
        InitializeComponent();

        YearComboBox.ItemsSource = Enumerable.Range(MinYear, MaxYear - MinYear + 1).ToList();
        MonthComboBox.ItemsSource = Enumerable.Range(1, 12).ToList();
        DayComboBox.ItemsSource = Enumerable.Range(1, 30).ToList();

        // 지금 선택된 양력 날짜를 음력으로 바꿔 초기값으로 둔다.
        var lunar = KoreanLunarDate.Convert(initialSolarDate);
        YearComboBox.SelectedItem = lunar?.Year ?? Math.Clamp(initialSolarDate.Year, MinYear, MaxYear);
        MonthComboBox.SelectedItem = lunar?.Month ?? 1;
        DayComboBox.SelectedItem = lunar?.Day ?? 1;
        LeapMonthCheckBox.IsChecked = lunar?.IsLeapMonth ?? false;

        _isLoading = false;
        UpdatePreview();
    }

    private void Input_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoading)
            UpdatePreview();
    }

    private void UpdatePreview()
    {
        SelectedSolarDate = Convert();

        if (SelectedSolarDate is { } solar)
        {
            SolarDateText.Text = solar.ToDateTime(TimeOnly.MinValue).ToString("yyyy년 M월 d일 (ddd)", Korean);
            SolarDateText.Foreground = System.Windows.Media.Brushes.Black;
            OkButton.IsEnabled = true;
        }
        else
        {
            SolarDateText.Text = "그 해에 없는 날짜입니다.";
            SolarDateText.Foreground = System.Windows.Media.Brushes.IndianRed;
            OkButton.IsEnabled = false;
        }
    }

    private DateOnly? Convert()
    {
        if (YearComboBox.SelectedItem is not int year ||
            MonthComboBox.SelectedItem is not int month ||
            DayComboBox.SelectedItem is not int day)
        {
            return null;
        }

        return KoreanLunarDate.ToSolar(year, month, day, LeapMonthCheckBox.IsChecked == true);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSolarDate is null)
            return;

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
