using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopCalendar.App;

/// <summary>
/// 위젯 바탕 색상/불투명도 조절 창. 슬라이더를 움직이면 아래 미리보기가 즉시 바뀐다
/// (미리보기는 실제 위젯과 같은 <see cref="WidgetTheme"/>를 쓰므로 결과가 그대로 보인다).
/// </summary>
public partial class AppearanceDialog : Window
{
    /// <summary>미리보기 뒤 그라데이션과 겹쳤을 때 색을 알아보기 쉽도록 두른 테두리 두께.</summary>
    private const double SwatchSize = 34;

    private bool _isLoading;

    public WidgetTheme SelectedTheme { get; private set; }

    public AppearanceDialog(WidgetTheme currentTheme)
    {
        InitializeComponent();

        SelectedTheme = currentTheme;
        BuildPresets();
        LoadTheme(currentTheme);
    }

    private void BuildPresets()
    {
        foreach (var (name, color) in WidgetTheme.Presets)
        {
            var swatch = new Border
            {
                Width = SwatchSize,
                Height = SwatchSize,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 6, 6),
                Background = new SolidColorBrush(color),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = name,
                Tag = color,
            };
            swatch.MouseLeftButtonDown += Preset_MouseLeftButtonDown;
            PresetPanel.Children.Add(swatch);
        }
    }

    private void Preset_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: Color color })
            return;

        _isLoading = true;
        RedSlider.Value = color.R;
        GreenSlider.Value = color.G;
        BlueSlider.Value = color.B;
        _isLoading = false;

        UpdatePreview();
    }

    private void LoadTheme(WidgetTheme theme)
    {
        _isLoading = true;
        RedSlider.Value = theme.PanelColor.R;
        GreenSlider.Value = theme.PanelColor.G;
        BlueSlider.Value = theme.PanelColor.B;
        OpacitySlider.Value = Math.Round(theme.Opacity * 100);
        _isLoading = false;

        UpdatePreview();
    }

    private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || !IsInitialized)
            return;

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var color = Color.FromRgb((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);
        var theme = new WidgetTheme(color, OpacitySlider.Value / 100.0);
        SelectedTheme = theme;

        RedValueText.Text = ((int)RedSlider.Value).ToString();
        GreenValueText.Text = ((int)GreenSlider.Value).ToString();
        BlueValueText.Text = ((int)BlueSlider.Value).ToString();
        OpacityValueText.Text = $"{(int)OpacitySlider.Value}%";

        PreviewPanel.Background = theme.PanelBrush;
        PreviewTitle.Foreground = theme.PrimaryText;
        PreviewWeekday.Foreground = theme.PrimaryText;
        PreviewSunday.Foreground = theme.SundayText;
        PreviewSaturday.Foreground = theme.SaturdayText;
        PreviewHoliday.Foreground = theme.HolidayText;
        PreviewHint.Foreground = theme.MutedText;
        PreviewChip.Background = new SolidColorBrush(Color.FromArgb(210, 70, 130, 200));
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e) =>
        LoadTheme(new WidgetTheme(WidgetTheme.DefaultPanelColor, WidgetTheme.DefaultOpacity));

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
