using System.Globalization;
using DesktopCalendar.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DesktopCalendar.Core.Google;

/// <summary>
/// 구글 이벤트 로컬 캐시 저장소 (DESIGN.md 4.4, 5).
/// 폴링 동기화 때 조회 구간 전체를 통째로 교체하는 단순한 전략을 쓴다.
/// </summary>
public sealed class GoogleEventRepository
{
    private readonly string _connectionString;

    public GoogleEventRepository(string? dbPath = null)
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
            CREATE TABLE IF NOT EXISTS GoogleEventCache (
                Id         TEXT NOT NULL,
                CalendarId TEXT NOT NULL,
                Title      TEXT NOT NULL,
                StartAt    TEXT NOT NULL,
                EndAt      TEXT NOT NULL,
                IsAllDay   INTEGER NOT NULL,
                FetchedAt  TEXT NOT NULL,
                PRIMARY KEY (Id, CalendarId)
            );
            CREATE INDEX IF NOT EXISTS IX_GoogleEventCache_StartAt ON GoogleEventCache(StartAt);
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<GoogleEvent> GetByMonth(int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        return GetOverlapping(monthStart, monthStart.AddMonths(1));
    }

    public IReadOnlyList<GoogleEvent> GetByDate(DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        return GetOverlapping(dayStart, dayStart.AddDays(1));
    }

    private IReadOnlyList<GoogleEvent> GetOverlapping(DateTime rangeStart, DateTime rangeEnd)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, CalendarId, Title, StartAt, EndAt, IsAllDay, FetchedAt
            FROM GoogleEventCache
            WHERE StartAt < $rangeEnd AND EndAt >= $rangeStart
            ORDER BY StartAt;
            """;
        command.Parameters.AddWithValue("$rangeStart", ToDbString(rangeStart));
        command.Parameters.AddWithValue("$rangeEnd", ToDbString(rangeEnd));

        var results = new List<GoogleEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new GoogleEvent
            {
                Id = reader.GetString(0),
                CalendarId = reader.GetString(1),
                Title = reader.GetString(2),
                StartAt = FromDbString(reader.GetString(3)),
                EndAt = FromDbString(reader.GetString(4)),
                IsAllDay = reader.GetInt32(5) != 0,
                FetchedAt = FromDbString(reader.GetString(6)),
            });
        }

        return results;
    }

    /// <summary>캐시 전체를 새로 받아온 이벤트로 교체한다.</summary>
    public void ReplaceAll(IReadOnlyList<GoogleEvent> events)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM GoogleEventCache;";
            delete.ExecuteNonQuery();
        }

        foreach (var googleEvent in events)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO GoogleEventCache (Id, CalendarId, Title, StartAt, EndAt, IsAllDay, FetchedAt)
                VALUES ($id, $calendarId, $title, $startAt, $endAt, $isAllDay, $fetchedAt)
                ON CONFLICT(Id, CalendarId) DO UPDATE SET
                    Title = excluded.Title, StartAt = excluded.StartAt, EndAt = excluded.EndAt,
                    IsAllDay = excluded.IsAllDay, FetchedAt = excluded.FetchedAt;
                """;
            insert.Parameters.AddWithValue("$id", googleEvent.Id);
            insert.Parameters.AddWithValue("$calendarId", googleEvent.CalendarId);
            insert.Parameters.AddWithValue("$title", googleEvent.Title);
            insert.Parameters.AddWithValue("$startAt", ToDbString(googleEvent.StartAt));
            insert.Parameters.AddWithValue("$endAt", ToDbString(googleEvent.EndAt));
            insert.Parameters.AddWithValue("$isAllDay", googleEvent.IsAllDay ? 1 : 0);
            insert.Parameters.AddWithValue("$fetchedAt", ToDbString(googleEvent.FetchedAt));
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>연결 해제 시 캐시를 비운다.</summary>
    public void Clear()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GoogleEventCache;";
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static string ToDbString(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime FromDbString(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
