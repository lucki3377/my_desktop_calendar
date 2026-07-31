using System.Windows;
using System.Windows.Controls;

namespace DesktopCalendar.App;

public partial class FontSizeDialog : Window
{
    public double SelectedScale { get; private set; }

    public FontSizeDialog(double currentScale)
    {
        InitializeComponent();
        ScaleSlider.Value = currentScale;
        UpdatePercentText(currentScale);
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdatePercentText(e.NewValue);

    private void UpdatePercentText(double scale) => PercentText.Text = $"{scale:P0}";

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedScale = ScaleSlider.Value;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
