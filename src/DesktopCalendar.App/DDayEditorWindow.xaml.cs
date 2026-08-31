using System.Globalization;
using System.Windows;
using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.App;

public partial class DDayEditorWindow : Window
{
    /// <summary>미리보기에 쓰는 요일 이름(일요일 시작).</summary>
    private static readonly string[] WeekdayLabels = ["일", "월", "화", "수", "목", "금", "토"];

    private readonly Guid? _originalId;
    private readonly bool _initialized;

    public DDay? Result { get; private set; }

    public DDayEditorWindow(DDay? existing)
    {
        InitializeComponent();
        _initialized = true;

        _originalId = existing?.Id;
        if (existing is not null)
        {
            TitleBox.Text = existing.Title;
            DatePickerControl.SelectedDate = existing.TargetDate.ToDateTime(TimeOnly.MinValue);
            RecurringCheckBox.IsChecked = existing.IsRecurringYearly;

            if (existing.IsOffsetBased)
            {
                OffsetModeRadio.IsChecked = true;
                BaseDatePicker.SelectedDate = existing.BaseDate!.Value.ToDateTime(TimeOnly.MinValue);
                OffsetDaysBox.Text = existing.OffsetDays!.Value.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                BaseDatePicker.SelectedDate = DateTime.Today;
            }
        }
        else
        {
            DatePickerControl.SelectedDate = DateTime.Today;
            BaseDatePicker.SelectedDate = DateTime.Today;
        }

        UpdatePreview();
    }

    private void ModeRadio_Checked(object sender, RoutedEventArgs e) => UpdatePreview();

    private void Input_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

    /// <summary>기준일 + 일수가 어떤 날짜가 되는지 즉시 보여준다.</summary>
    private void UpdatePreview()
    {
        // XAML 파싱 중 IsChecked="True"로 이벤트가 먼저 불릴 수 있어 컨트롤이 아직 없을 수 있다.
        if (!_initialized)
            return;

        // 직접 지정 모드에서는 기준일 입력 안내를 띄우지 않는다 (해당 칸이 비활성 상태라 혼란만 준다).
        var offsetMode = OffsetModeRadio.IsChecked == true;

        if (!TryGetOffsetTarget(out var target, out var error))
        {
            PreviewText.Text = offsetMode ? error : string.Empty;
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var remaining = DDayCalculator.ComputeDaysRemaining(
            new DDay
            {
                Title = string.Empty,
                TargetDate = target,
                IsRecurringYearly = RecurringCheckBox.IsChecked == true,
            },
            today);

        PreviewText.Text = $"→ {target:yyyy-MM-dd}({WeekdayLabels[(int)target.DayOfWeek]})  {DDayCalculator.Format(remaining)}";
    }

    /// <summary>기준일 방식 입력값을 검증하고 대상 날짜를 계산한다.</summary>
    private bool TryGetOffsetTarget(out DateOnly target, out string error)
    {
        target = default;
        error = string.Empty;

        if (BaseDatePicker.SelectedDate is not DateTime baseDate)
        {
            error = "기준일을 선택하세요.";
            return false;
        }

        var text = OffsetDaysBox.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            error = "일수를 입력하세요.";
            return false;
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offsetDays))
        {
            error = "일수는 숫자로 입력하세요. (기준일 이전이면 음수)";
            return false;
        }

        if (!DDayCalculator.TryComputeTargetFromBase(DateOnly.FromDateTime(baseDate), offsetDays, out target))
        {
            error = "일수가 너무 커서 날짜를 계산할 수 없습니다.";
            return false;
        }

        return true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show("제목을 입력하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DateOnly targetDate;
        DateOnly? storedBaseDate = null;
        int? storedOffsetDays = null;

        if (OffsetModeRadio.IsChecked == true)
        {
            if (!TryGetOffsetTarget(out targetDate, out var error))
            {
                MessageBox.Show(error, "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            storedBaseDate = DateOnly.FromDateTime(BaseDatePicker.SelectedDate!.Value);
            storedOffsetDays = targetDate.DayNumber - storedBaseDate.Value.DayNumber;
        }
        else
        {
            if (DatePickerControl.SelectedDate is not DateTime date)
            {
                MessageBox.Show("날짜를 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            targetDate = DateOnly.FromDateTime(date);
        }

        Result = new DDay
        {
            Id = _originalId ?? Guid.NewGuid(),
            Title = TitleBox.Text.Trim(),
            TargetDate = targetDate,
            IsRecurringYearly = RecurringCheckBox.IsChecked == true,
            BaseDate = storedBaseDate,
            OffsetDays = storedOffsetDays,
        };

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
