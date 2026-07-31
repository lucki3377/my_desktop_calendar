using System.Windows;

namespace DesktopCalendar.App;

public partial class MonthPickerWindow : Window
{
    public int SelectedYear { get; private set; }
    public int SelectedMonth { get; private set; }

    public MonthPickerWindow(int year, int month)
    {
        InitializeComponent();

        var currentYear = DateTime.Today.Year;
        for (var y = currentYear - 10; y <= currentYear + 10; y++)
            YearComboBox.Items.Add(y);

        for (var m = 1; m <= 12; m++)
            MonthComboBox.Items.Add(m);

        YearComboBox.SelectedItem = year;
        MonthComboBox.SelectedItem = month;
    }

    private void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        YearComboBox.SelectedItem = DateTime.Today.Year;
        MonthComboBox.SelectedItem = DateTime.Today.Month;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (YearComboBox.SelectedItem is not int year || MonthComboBox.SelectedItem is not int month)
        {
            MessageBox.Show("년/월을 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedYear = year;
        SelectedMonth = month;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
