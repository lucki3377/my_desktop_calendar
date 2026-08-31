using System.Windows;
using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.App;

public partial class DDayListWindow : Window
{
    private readonly DDayRepository _repository;

    public DDayListWindow(DDayRepository repository)
    {
        InitializeComponent();
        _repository = repository;
        LoadList();
    }

    private void LoadList()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = _repository.GetAll()
            .Select(d => new DDayListItem(d, Format(d, today)))
            .OrderBy(i => DDayCalculator.ComputeDaysRemaining(i.DDay, today))
            .ToList();

        DDayListBox.ItemsSource = items;
    }

    private static string Format(DDay dday, DateOnly today)
    {
        var remaining = DDayCalculator.ComputeDaysRemaining(dday, today);
        var recurring = dday.IsRecurringYearly ? " (매년)" : "";
        var origin = dday.IsOffsetBased
            ? $" [{dday.TargetDate:yyyy-MM-dd} ← {dday.BaseDate!.Value:yyyy-MM-dd} {dday.OffsetDays!.Value:+#;-#;+0}일]"
            : $" [{dday.TargetDate:yyyy-MM-dd}]";
        return $"{DDayCalculator.Format(remaining)}  {dday.Title}{recurring}{origin}";
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new DDayEditorWindow(null);
        if (editor.ShowDialog() == true && editor.Result is not null)
        {
            _repository.Add(editor.Result);
            LoadList();
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (DDayListBox.SelectedItem is not DDayListItem item)
        {
            MessageBox.Show("수정할 항목을 목록에서 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new DDayEditorWindow(item.DDay);
        if (editor.ShowDialog() == true && editor.Result is not null)
        {
            _repository.Update(editor.Result);
            LoadList();
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (DDayListBox.SelectedItem is not DDayListItem item)
        {
            MessageBox.Show("삭제할 항목을 목록에서 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show($"'{item.DDay.Title}' 항목을 삭제할까요?", "삭제 확인",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            _repository.Delete(item.DDay.Id);
            LoadList();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record DDayListItem(DDay DDay, string Display)
    {
        public override string ToString() => Display;
    }
}
