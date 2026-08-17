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
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS DDay (
                Id               TEXT PRIMARY KEY NOT NULL,
                Title            TEXT NOT NULL,
                TargetDate       TEXT NOT NULL,
                IsRecurringYearly INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<DDay> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, TargetDate, IsRecurringYearly FROM DDay ORDER BY TargetDate;";

        var results = new List<DDay>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new DDay
            {
                Id = Guid.Parse(reader.GetString(0)),
                Title = reader.GetString(1),
                TargetDate = DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                IsRecurringYearly = reader.GetInt32(3) != 0,
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
            INSERT INTO DDay (Id, Title, TargetDate, IsRecurringYearly)
            VALUES ($id, $title, $targetDate, $isRecurringYearly)
            ON CONFLICT(Id) DO UPDATE SET
                Title = excluded.Title, TargetDate = excluded.TargetDate,
                IsRecurringYearly = excluded.IsRecurringYearly;
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
            INSERT INTO DDay (Id, Title, TargetDate, IsRecurringYearly)
            VALUES ($id, $title, $targetDate, $isRecurringYearly);
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
            UPDATE DDay SET Title = $title, TargetDate = $targetDate, IsRecurringYearly = $isRecurringYearly
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

    private static void BindParameters(SqliteCommand command, DDay dday)
    {
        command.Parameters.AddWithValue("$id", dday.Id.ToString());
        command.Parameters.AddWithValue("$title", dday.Title);
        command.Parameters.AddWithValue("$targetDate", dday.TargetDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$isRecurringYearly", dday.IsRecurringYearly ? 1 : 0);
    }
}
