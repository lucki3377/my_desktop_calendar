using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace DesktopCalendar.App;

/// <summary>백업 저장 / 복원 / .ics 내보내기 창 (DESIGN.md 4.12).</summary>
public partial class BackupWindow : Window
{
    private const string JsonFilter = "달력 백업 파일 (*.json)|*.json|모든 파일 (*.*)|*.*";
    private const string IcsFilter = "iCalendar 파일 (*.ics)|*.ics|모든 파일 (*.*)|*.*";

    private readonly BackupService _backupService;

    /// <summary>복원으로 데이터가 바뀌었는지. 위젯이 다시 그릴지 판단하는 데 쓴다.</summary>
    public bool DataChanged { get; private set; }

    public BackupWindow(BackupService backupService)
    {
        InitializeComponent();
        _backupService = backupService;
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = JsonFilter,
            FileName = BackupService.SuggestedFileName("json"),
        };

        if (dialog.ShowDialog() != true)
            return;

        Run(() =>
        {
            _backupService.ExportJson(dialog.FileName);
            ShowStatus($"백업을 저장했습니다: {Path.GetFileName(dialog.FileName)}");
        });
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = JsonFilter, CheckFileExists = true };
        if (dialog.ShowDialog() != true)
            return;

        var confirm = MessageBox.Show(
            "백업 내용을 지금 데이터에 합칩니다.\n같은 항목은 백업 내용으로 덮어씁니다. 계속할까요?",
            "복원 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        Run(() =>
        {
            var result = _backupService.ImportJson(dialog.FileName);
            DataChanged = true;
            ShowStatus($"복원했습니다 — 일정 {result.Schedules}개, D-day {result.DDays}개, 공휴일 {result.ManualHolidays}개.");
        });
    }

    private void ExportIcsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = IcsFilter,
            FileName = BackupService.SuggestedFileName("ics"),
        };

        if (dialog.ShowDialog() != true)
            return;

        Run(() =>
        {
            _backupService.ExportIcs(dialog.FileName);
            ShowStatus($"내보냈습니다: {Path.GetFileName(dialog.FileName)}");
        });
    }

    /// <summary>파일 작업에서 흔히 나는 오류를 붙잡아 안내로 바꾼다.</summary>
    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or JsonException or InvalidDataException)
        {
            StatusText.Text = string.Empty;
            MessageBox.Show(ex.Message, "실패", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowStatus(string message) => StatusText.Text = message;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
