using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopCalendar.Core.Calendar;
using DesktopCalendar.Core.Holiday;

namespace DesktopCalendar.App;

/// <summary>백업 파일에 담기는 내용 (DESIGN.md 4.12).</summary>
public sealed class BackupData
{
    /// <summary>형식이 바뀌면 올린다. 복원할 때 읽을 수 있는 형식인지 확인하는 용도.</summary>
    public int Version { get; set; } = BackupService.CurrentVersion;

    public DateTime ExportedAt { get; set; } = DateTime.Now;

    public List<Schedule> Schedules { get; set; } = [];
    public List<DDay> DDays { get; set; } = [];

    /// <summary>사용자가 직접 지정한 공휴일만. API/내장 계산본은 다시 만들 수 있어 담지 않는다.</summary>
    public List<Holiday> ManualHolidays { get; set; } = [];
}

/// <summary>복원 결과 요약.</summary>
public sealed record BackupImportResult(int Schedules, int DDays, int ManualHolidays)
{
    public int Total => Schedules + DDays + ManualHolidays;
}

/// <summary>
/// 일정·D-day·수동 공휴일을 파일로 내보내고 되돌리는 기능 (DESIGN.md 4.12).
/// 데이터가 `%AppData%\DesktopCalendar\calendar.db` 한 곳에만 있어서, PC를 갈아엎으면
/// 그대로 사라진다는 문제를 덜기 위한 장치다.
/// </summary>
public sealed class BackupService(
    ScheduleRepository scheduleRepository,
    DDayRepository dDayRepository,
    HolidayRepository holidayRepository)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public void ExportJson(string path)
    {
        var data = new BackupData
        {
            Schedules = [.. scheduleRepository.GetAll()],
            DDays = [.. dDayRepository.GetAll()],
            ManualHolidays = [.. holidayRepository.GetManual()],
        };

        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8);
    }

    /// <summary>
    /// 백업을 현재 데이터에 합친다. 같은 항목(Id/날짜)은 백업 내용으로 덮어쓰고,
    /// 백업에 없는 기존 항목은 지우지 않는다 — 실수로 복원해도 데이터가 날아가지 않게.
    /// </summary>
    public BackupImportResult ImportJson(string path)
    {
        var json = File.ReadAllText(path, Encoding.UTF8);
        var data = JsonSerializer.Deserialize<BackupData>(json, JsonOptions)
            ?? throw new InvalidDataException("백업 파일을 읽지 못했습니다.");

        if (data.Version > CurrentVersion)
            throw new InvalidDataException(
                $"이 백업은 더 새로운 버전({data.Version})입니다. 앱을 최신으로 올린 뒤 다시 시도하세요.");

        foreach (var schedule in data.Schedules)
            scheduleRepository.Upsert(schedule);

        foreach (var dday in data.DDays)
            dDayRepository.Upsert(dday);

        foreach (var holiday in data.ManualHolidays)
            holidayRepository.AddManual(holiday.Date, holiday.Name);

        return new BackupImportResult(data.Schedules.Count, data.DDays.Count, data.ManualHolidays.Count);
    }

    /// <summary>다른 캘린더 앱에서 열 수 있는 .ics로 내보낸다 (일정만).</summary>
    public void ExportIcs(string path) =>
        File.WriteAllText(path, IcsExporter.Export(scheduleRepository.GetAll()), Encoding.UTF8);

    /// <summary>파일 이름에 쓰기 좋은 오늘 날짜 접미사.</summary>
    public static string SuggestedFileName(string extension) =>
        $"DesktopCalendar-{DateTime.Now:yyyyMMdd}.{extension}";
}
