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
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Schedule (
                Id          TEXT PRIMARY KEY NOT NULL,
                Title       TEXT NOT NULL,
                Description TEXT NULL,
                StartAt     TEXT NOT NULL,
                EndAt       TEXT NOT NULL,
                IsAllDay    INTEGER NOT NULL,
                Color       TEXT NULL,
                CreatedAt   TEXT NOT NULL,
                UpdatedAt   TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Schedule_StartAt ON Schedule(StartAt);
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<Schedule> GetByMonth(int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);
        return GetOverlapping(monthStart, monthEnd);
    }

    public IReadOnlyList<Schedule> GetByDate(DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        return GetOverlapping(dayStart, dayEnd);
    }

    private IReadOnlyList<Schedule> GetOverlapping(DateTime rangeStart, DateTime rangeEnd)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Title, Description, StartAt, EndAt, IsAllDay, Color, CreatedAt, UpdatedAt
            FROM Schedule
            WHERE StartAt < $rangeEnd AND EndAt >= $rangeStart
            ORDER BY StartAt;
            """;
        command.Parameters.AddWithValue("$rangeStart", ToDbString(rangeStart));
        command.Parameters.AddWithValue("$rangeEnd", ToDbString(rangeEnd));

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
            INSERT INTO Schedule (Id, Title, Description, StartAt, EndAt, IsAllDay, Color, CreatedAt, UpdatedAt)
            VALUES ($id, $title, $description, $startAt, $endAt, $isAllDay, $color, $createdAt, $updatedAt);
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
                IsAllDay = $isAllDay, Color = $color, UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        BindParameters(command, schedule);
        command.ExecuteNonQuery();
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
        command.Parameters.AddWithValue("$createdAt", ToDbString(schedule.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", ToDbString(schedule.UpdatedAt));
    }

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
    };

    private static string ToDbString(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime FromDbString(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
