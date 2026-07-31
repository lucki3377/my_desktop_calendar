using System.Windows;
using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.App;

public partial class DDayEditorWindow : Window
{
    private readonly Guid? _originalId;

    public DDay? Result { get; private set; }

    public DDayEditorWindow(DDay? existing)
    {
        InitializeComponent();

        _originalId = existing?.Id;
        if (existing is not null)
        {
            TitleBox.Text = existing.Title;
            DatePickerControl.SelectedDate = existing.TargetDate.ToDateTime(TimeOnly.MinValue);
            RecurringCheckBox.IsChecked = existing.IsRecurringYearly;
        }
        else
        {
            DatePickerControl.SelectedDate = DateTime.Today;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show("제목을 입력하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DatePickerControl.SelectedDate is not DateTime date)
        {
            MessageBox.Show("날짜를 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new DDay
        {
            Id = _originalId ?? Guid.NewGuid(),
            Title = TitleBox.Text.Trim(),
            TargetDate = DateOnly.FromDateTime(date),
            IsRecurringYearly = RecurringCheckBox.IsChecked == true,
        };

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
