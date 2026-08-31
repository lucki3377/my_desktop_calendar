using System.Globalization;
using Microsoft.Data.Sqlite;
using DesktopCalendar.Core.Storage;

namespace DesktopCalendar.Core.Calendar;

/// <summary>D-day 항목의 SQLite CRUD (DESIGN.md 4.6, 5).</summary>
public sealed class DDayRepository
{
    private readonly string _connectionString;

    public DDayRepository(string? dbPath = null)
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
                CREATE TABLE IF NOT EXISTS DDay (
                    Id               TEXT PRIMARY KEY NOT NULL,
                    Title            TEXT NOT NULL,
                    TargetDate       TEXT NOT NULL,
                    IsRecurringYearly INTEGER NOT NULL,
                    BaseDate         TEXT NULL,
                    OffsetDays       INTEGER NULL
                );
                """;
            command.ExecuteNonQuery();
        }

        // 이전 버전에서 만들어진 DB에는 없는 열들 (2026-08-31 추가 — 기준일 + N일 방식).
        AddColumnIfMissing(connection, "BaseDate", "TEXT NULL");
        AddColumnIfMissing(connection, "OffsetDays", "INTEGER NULL");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string columnName, string columnType)
    {
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT 1 FROM pragma_table_info('DDay') WHERE name = $name;";
            check.Parameters.AddWithValue("$name", columnName);
            if (check.ExecuteScalar() is not null)
                return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE DDay ADD COLUMN {columnName} {columnType};";
        alter.ExecuteNonQuery();
    }

    public IReadOnlyList<DDay> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Title, TargetDate, IsRecurringYearly, BaseDate, OffsetDays FROM DDay ORDER BY TargetDate;";

        var results = new List<DDay>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new DDay
            {
                Id = Guid.Parse(reader.GetString(0)),
                Title = reader.GetString(1),
                TargetDate = ParseDate(reader.GetString(2)),
                IsRecurringYearly = reader.GetInt32(3) != 0,
                BaseDate = reader.IsDBNull(4) ? null : ParseDate(reader.GetString(4)),
                OffsetDays = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            });
        }

        return results;
    }

    /// <summary>백업 복원용 — 같은 Id가 있으면 덮어쓴다.</summary>
    public void Upsert(DDay dday)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO DDay (Id, Title, TargetDate, IsRecurringYearly, BaseDate, OffsetDays)
            VALUES ($id, $title, $targetDate, $isRecurringYearly, $baseDate, $offsetDays)
            ON CONFLICT(Id) DO UPDATE SET
                Title = excluded.Title, TargetDate = excluded.TargetDate,
                IsRecurringYearly = excluded.IsRecurringYearly,
                BaseDate = excluded.BaseDate, OffsetDays = excluded.OffsetDays;
            """;
        BindParameters(command, dday);
        command.ExecuteNonQuery();
    }

    public DDay Add(DDay dday)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO DDay (Id, Title, TargetDate, IsRecurringYearly, BaseDate, OffsetDays)
            VALUES ($id, $title, $targetDate, $isRecurringYearly, $baseDate, $offsetDays);
            """;
        BindParameters(command, dday);
        command.ExecuteNonQuery();
        return dday;
    }

    public void Update(DDay dday)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE DDay SET Title = $title, TargetDate = $targetDate, IsRecurringYearly = $isRecurringYearly,
                BaseDate = $baseDate, OffsetDays = $offsetDays
            WHERE Id = $id;
            """;
        BindParameters(command, dday);
        command.ExecuteNonQuery();
    }

    public void Delete(Guid id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DDay WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static DateOnly ParseDate(string value)
        => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void BindParameters(SqliteCommand command, DDay dday)
    {
        command.Parameters.AddWithValue("$id", dday.Id.ToString());
        command.Parameters.AddWithValue("$title", dday.Title);
        command.Parameters.AddWithValue("$targetDate", dday.TargetDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$isRecurringYearly", dday.IsRecurringYearly ? 1 : 0);

        // 기준일 방식이 아니면 두 열 모두 NULL로 저장한다 (한쪽만 채워지는 상태를 만들지 않는다).
        var isOffsetBased = dday.IsOffsetBased;
        command.Parameters.AddWithValue("$baseDate",
            isOffsetBased ? dday.BaseDate!.Value.ToString("yyyy-MM-dd") : DBNull.Value);
        command.Parameters.AddWithValue("$offsetDays",
            isOffsetBased ? dday.OffsetDays!.Value : DBNull.Value);
    }
}
