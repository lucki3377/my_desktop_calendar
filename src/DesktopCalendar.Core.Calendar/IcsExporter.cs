using System.Globalization;
using System.Text;

namespace DesktopCalendar.Core.Calendar;

/// <summary>
/// 로컬 일정을 iCalendar(.ics) 텍스트로 내보낸다 (DESIGN.md 4.12).
/// 다른 캘린더 앱(구글 캘린더, 아웃룩 등)에서 열 수 있는 표준 형식이라 백업 겸 이전 수단이 된다.
/// 반복 일정은 회차를 일일이 늘어놓지 않고 RRULE 한 줄로 내보낸다.
/// </summary>
public static class IcsExporter
{
    /// <summary>iCalendar 규격은 줄 끝을 CRLF로 요구한다.</summary>
    private const string LineBreak = "\r\n";

    /// <summary>한 줄이 75옥텟을 넘으면 접어야 한다(RFC 5545). 넉넉하게 73자로 자른다.</summary>
    private const int MaxLineLength = 73;

    public static string Export(IEnumerable<Schedule> schedules, DateTime? now = null)
    {
        var stamp = FormatUtc(now ?? DateTime.UtcNow);
        var builder = new StringBuilder();

        AppendLine(builder, "BEGIN:VCALENDAR");
        AppendLine(builder, "VERSION:2.0");
        AppendLine(builder, "PRODID:-//DesktopCalendar//KO");
        AppendLine(builder, "CALSCALE:GREGORIAN");

        foreach (var schedule in schedules)
            AppendEvent(builder, schedule, stamp);

        AppendLine(builder, "END:VCALENDAR");
        return builder.ToString();
    }

    private static void AppendEvent(StringBuilder builder, Schedule schedule, string stamp)
    {
        AppendLine(builder, "BEGIN:VEVENT");
        AppendLine(builder, $"UID:{schedule.Id}@desktopcalendar");
        AppendLine(builder, $"DTSTAMP:{stamp}");
        AppendLine(builder, $"SUMMARY:{Escape(schedule.Title)}");

        if (!string.IsNullOrWhiteSpace(schedule.Description))
            AppendLine(builder, $"DESCRIPTION:{Escape(schedule.Description)}");

        if (schedule.IsAllDay)
        {
            // 종일 일정의 DTEND는 배타적이므로 마지막 날 다음 날을 적는다.
            AppendLine(builder, $"DTSTART;VALUE=DATE:{FormatDate(schedule.StartAt)}");
            AppendLine(builder, $"DTEND;VALUE=DATE:{FormatDate(schedule.EndAt.AddDays(1))}");
        }
        else
        {
            AppendLine(builder, $"DTSTART:{FormatLocal(schedule.StartAt)}");
            AppendLine(builder, $"DTEND:{FormatLocal(schedule.EndAt)}");
        }

        if (schedule.IsRecurring)
        {
            AppendLine(builder, BuildRecurrenceRule(schedule));

            if (schedule.RecurrenceExceptions.Count > 0)
                AppendLine(builder, BuildExceptionDates(schedule));
        }

        AppendLine(builder, "END:VEVENT");
    }

    private static string BuildRecurrenceRule(Schedule schedule)
    {
        var frequency = schedule.Recurrence switch
        {
            RecurrenceType.Daily => "DAILY",
            RecurrenceType.Weekly => "WEEKLY",
            RecurrenceType.Monthly => "MONTHLY",
            RecurrenceType.Yearly => "YEARLY",
            _ => "DAILY",
        };

        var rule = $"RRULE:FREQ={frequency}";
        if (schedule.RecurrenceUntil is { } until)
            rule += $";UNTIL={until:yyyyMMdd}T235959";

        return rule;
    }

    private static string BuildExceptionDates(Schedule schedule)
    {
        // 제외 날짜는 DTSTART와 같은 형식이어야 파서가 회차를 맞춰 찾는다.
        var values = schedule.RecurrenceExceptions.Select(date => schedule.IsAllDay
            ? date.ToString("yyyyMMdd")
            : date.ToDateTime(TimeOnly.FromDateTime(schedule.StartAt)).ToString("yyyyMMdd'T'HHmmss"));

        var prefix = schedule.IsAllDay ? "EXDATE;VALUE=DATE:" : "EXDATE:";
        return prefix + string.Join(',', values);
    }

    private static string FormatDate(DateTime value) => value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static string FormatLocal(DateTime value) =>
        value.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static string FormatUtc(DateTime value) =>
        value.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>iCalendar 텍스트 값에서 특별한 뜻을 갖는 문자를 이스케이프한다.</summary>
    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n")
        .Replace("\r", "\\n");

    /// <summary>긴 줄은 다음 줄 맨 앞에 공백을 두고 이어 붙인다(RFC 5545의 folding).</summary>
    private static void AppendLine(StringBuilder builder, string line)
    {
        if (line.Length <= MaxLineLength)
        {
            builder.Append(line).Append(LineBreak);
            return;
        }

        builder.Append(line[..MaxLineLength]).Append(LineBreak);

        var rest = line[MaxLineLength..];
        while (rest.Length > 0)
        {
            var take = Math.Min(MaxLineLength - 1, rest.Length);
            builder.Append(' ').Append(rest[..take]).Append(LineBreak);
            rest = rest[take..];
        }
    }
}
