using System.Globalization;
using Microsoft.Data.Sqlite;
using DesktopCalendar.Core.Storage;

namespace DesktopCalendar.Core.Calendar;

/// <summary>
/// 로컬 일정(Schedule)의 SQLite CRUD (DESIGN.md 4.3, 5).
/// </summary>
public sealed class ScheduleRepository
{
    private readonly string _connectionString;

    public ScheduleRepository(string? dbPath = null)
    {
        dbPath ??= AppPaths.DatabaseFilePath;
        _connectionString = $"Data Source={dbPath}";
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var connection = Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS Schedule (
                    Id                    TEXT PRIMARY KEY NOT NULL,
                    Title                 TEXT NOT NULL,
                    Description           TEXT NULL,
                    StartAt               TEXT NOT NULL,
                    EndAt                 TEXT NOT NULL,
                    IsAllDay              INTEGER NOT NULL,
                    Color                 TEXT NULL,
                    ReminderMinutesBefore INTEGER NULL,
                    Recurrence            TEXT NULL,
                    RecurrenceUntil       TEXT NULL,
                    RecurrenceExceptions  TEXT NULL,
                    CreatedAt             TEXT NOT NULL,
                    UpdatedAt             TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Schedule_StartAt ON Schedule(StartAt);
                """;
            command.ExecuteNonQuery();
        }

        // 이전 버전에서 만들어진 DB에는 없는 열들 (2026-08-17 추가).
        AddColumnIfMissing(connection, "ReminderMinutesBefore", "INTEGER NULL");
        AddColumnIfMissing(connection, "Recurrence", "TEXT NULL");
        AddColumnIfMissing(connection, "RecurrenceUntil", "TEXT NULL");
        AddColumnIfMissing(connection, "RecurrenceExceptions", "TEXT NULL");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string columnName, string columnType)
    {
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT 1 FROM pragma_table_info('Schedule') WHERE name = $name;";
            check.Parameters.AddWithValue("$name", columnName);
            if (check.ExecuteScalar() is not null)
                return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE Schedule ADD COLUMN {columnName} {columnType};";
        alter.ExecuteNonQuery();
    }

    /// <summary>그 달에 실제로 열리는 회차들 (반복 일정은 펼쳐서 돌려준다).</summary>
    public IReadOnlyList<ScheduleOccurrence> GetOccurrencesByMonth(int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        return GetOccurrences(monthStart, monthStart.AddMonths(1));
    }

    /// <summary>그 날짜에 열리는 회차들.</summary>
    public IReadOnlyList<ScheduleOccurrence> GetOccurrencesByDate(DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        return GetOccurrences(dayStart, dayStart.AddDays(1));
    }

    private IReadOnlyList<ScheduleOccurrence> GetOccurrences(DateTime rangeStart, DateTime rangeEnd) =>
        RecurrenceExpander.ExpandAll(GetCandidates(rangeStart, rangeEnd), rangeStart, rangeEnd);

    /// <summary>
    /// 구간에 걸릴 <i>가능성이</i> 있는 일정을 넓게 읽어온다. 반복 일정은 시작이 한참 전이어도
    /// 지금 구간에 열릴 수 있으므로, 끝난 반복(RecurrenceUntil이 지난 것)만 걸러낸다.
    /// 실제로 열리는지는 <see cref="RecurrenceExpander"/>가 판단한다.
    /// </summary>
    private IReadOnlyList<Schedule> GetCandidates(DateTime rangeStart, DateTime rangeEnd)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT {SelectColumns}
            FROM Schedule
            WHERE StartAt < $rangeEnd
              AND (
                    (COALESCE(Recurrence, 'None') = 'None' AND EndAt >= $rangeStart)
                 OR (COALESCE(Recurrence, 'None') <> 'None'
                     AND (RecurrenceUntil IS NULL OR RecurrenceUntil >= $rangeStartDate))
              )
            ORDER BY StartAt;
            """;
        command.Parameters.AddWithValue("$rangeStart", ToDbString(rangeStart));
        command.Parameters.AddWithValue("$rangeEnd", ToDbString(rangeEnd));
        command.Parameters.AddWithValue("$rangeStartDate", ToDbDate(DateOnly.FromDateTime(rangeStart)));

        var results = new List<Schedule>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            results.Add(ReadSchedule(reader));

        return results;
    }

    public Schedule Add(Schedule schedule)
    {
        schedule.CreatedAt = DateTime.Now;
        schedule.UpdatedAt = schedule.CreatedAt;

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Schedule (Id, Title, Description, StartAt, EndAt, IsAllDay, Color, ReminderMinutesBefore,
                                  Recurrence, RecurrenceUntil, RecurrenceExceptions, CreatedAt, UpdatedAt)
            VALUES ($id, $title, $description, $startAt, $endAt, $isAllDay, $color, $reminderMinutesBefore,
                    $recurrence, $recurrenceUntil, $recurrenceExceptions, $createdAt, $updatedAt);
            """;
        BindParameters(command, schedule);
        command.ExecuteNonQuery();
        return schedule;
    }

    public void Update(Schedule schedule)
    {
        schedule.UpdatedAt = DateTime.Now;

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Schedule
            SET Title = $title, Description = $description, StartAt = $startAt, EndAt = $endAt,
                IsAllDay = $isAllDay, Color = $color, ReminderMinutesBefore = $reminderMinutesBefore,
                Recurrence = $recurrence, RecurrenceUntil = $recurrenceUntil,
                RecurrenceExceptions = $recurrenceExceptions, UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        BindParameters(command, schedule);
        command.ExecuteNonQuery();
    }

    public Schedule? GetById(Guid id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM Schedule WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSchedule(reader) : null;
    }

    /// <summary>반복 일정에서 특정 회차 하루만 빼고 싶을 때 (그 날짜를 제외 목록에 넣는다).</summary>
    public void AddRecurrenceException(Guid id, DateOnly date)
    {
        var schedule = GetById(id);
        if (schedule is null || schedule.RecurrenceExceptions.Contains(date))
            return;

        schedule.RecurrenceExceptions = [.. schedule.RecurrenceExceptions, date];
        Update(schedule);
    }

    public void Delete(Guid id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Schedule WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void BindParameters(SqliteCommand command, Schedule schedule)
    {
        command.Parameters.AddWithValue("$id", schedule.Id.ToString());
        command.Parameters.AddWithValue("$title", schedule.Title);
        command.Parameters.AddWithValue("$description", (object?)schedule.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$startAt", ToDbString(schedule.StartAt));
        command.Parameters.AddWithValue("$endAt", ToDbString(schedule.EndAt));
        command.Parameters.AddWithValue("$isAllDay", schedule.IsAllDay ? 1 : 0);
        command.Parameters.AddWithValue("$color", (object?)schedule.Color ?? DBNull.Value);
        command.Parameters.AddWithValue("$reminderMinutesBefore", (object?)schedule.ReminderMinutesBefore ?? DBNull.Value);
        command.Parameters.AddWithValue("$recurrence", schedule.Recurrence.ToString());
        command.Parameters.AddWithValue("$recurrenceUntil",
            schedule.RecurrenceUntil is { } until ? ToDbDate(until) : DBNull.Value);
        command.Parameters.AddWithValue("$recurrenceExceptions",
            schedule.RecurrenceExceptions.Count == 0
                ? DBNull.Value
                : string.Join(',', schedule.RecurrenceExceptions.Select(ToDbDate)));
        command.Parameters.AddWithValue("$createdAt", ToDbString(schedule.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", ToDbString(schedule.UpdatedAt));
    }

    private const string SelectColumns =
        "Id, Title, Description, StartAt, EndAt, IsAllDay, Color, CreatedAt, UpdatedAt, " +
        "ReminderMinutesBefore, Recurrence, RecurrenceUntil, RecurrenceExceptions";

    private static Schedule ReadSchedule(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        Title = reader.GetString(1),
        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
        StartAt = FromDbString(reader.GetString(3)),
        EndAt = FromDbString(reader.GetString(4)),
        IsAllDay = reader.GetInt32(5) != 0,
        Color = reader.IsDBNull(6) ? null : reader.GetString(6),
        CreatedAt = FromDbString(reader.GetString(7)),
        UpdatedAt = FromDbString(reader.GetString(8)),
        ReminderMinutesBefore = reader.IsDBNull(9) ? null : reader.GetInt32(9),
        Recurrence = reader.IsDBNull(10) ? RecurrenceType.None : ParseRecurrence(reader.GetString(10)),
        RecurrenceUntil = reader.IsDBNull(11) ? null : FromDbDate(reader.GetString(11)),
        RecurrenceExceptions = reader.IsDBNull(12) ? [] : ParseDates(reader.GetString(12)),
    };

    /// <summary>
    /// 알림이 걸린 일정 후보. 반복 일정은 원래 시작 시각이 한참 전이어도 지금 알림이 필요할 수 있으므로,
    /// 끝나지 않은 반복은 전부 후보로 넘기고 실제 회차 계산은 호출 쪽(<see cref="RecurrenceExpander"/>)에 맡긴다.
    /// </summary>
    public IReadOnlyList<Schedule> GetReminderCandidates(DateTime fromStartAt, DateTime toStartAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT {SelectColumns}
            FROM Schedule
            WHERE ReminderMinutesBefore IS NOT NULL
              AND (
                    (COALESCE(Recurrence, 'None') = 'None' AND StartAt >= $from AND StartAt <= $to)
                 OR (COALESCE(Recurrence, 'None') <> 'None'
                     AND StartAt <= $to
                     AND (RecurrenceUntil IS NULL OR RecurrenceUntil >= $fromDate))
              )
            ORDER BY StartAt;
            """;
        command.Parameters.AddWithValue("$from", ToDbString(fromStartAt));
        command.Parameters.AddWithValue("$to", ToDbString(toStartAt));
        command.Parameters.AddWithValue("$fromDate", ToDbDate(DateOnly.FromDateTime(fromStartAt)));

        var results = new List<Schedule>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            results.Add(ReadSchedule(reader));

        return results;
    }

    private static RecurrenceType ParseRecurrence(string value) =>
        Enum.TryParse<RecurrenceType>(value, out var parsed) ? parsed : RecurrenceType.None;

    private static IReadOnlyList<DateOnly> ParseDates(string value) =>
        [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(FromDbDate)];

    private static string ToDbString(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime FromDbString(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string ToDbDate(DateOnly value) => value.ToString("yyyy-MM-dd");

    private static DateOnly FromDbDate(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
