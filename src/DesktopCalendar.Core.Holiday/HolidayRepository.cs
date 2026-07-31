using System.Globalization;
using Microsoft.Data.Sqlite;
using DesktopCalendar.Core.Storage;

namespace DesktopCalendar.Core.Holiday;

/// <summary>
/// 공휴일 로컬 캐시 + 수동 추가/제외 저장소 (DESIGN.md 4.2, 5).
/// </summary>
public sealed class HolidayRepository
{
    private readonly string _connectionString;

    public HolidayRepository(string? dbPath = null)
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
            CREATE TABLE IF NOT EXISTS Holiday (
                Date   TEXT PRIMARY KEY NOT NULL,
                Name   TEXT NOT NULL,
                Kind   TEXT NOT NULL,
                Source TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS HolidayCachedYear (
                Year INTEGER PRIMARY KEY NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public bool IsYearCached(int year)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM HolidayCachedYear WHERE Year = $year;";
        command.Parameters.AddWithValue("$year", year);
        return command.ExecuteScalar() is not null;
    }

    /// <summary>API에서 받아온 연도별 공휴일로 캐시를 갱신한다 (해당 연도의 기존 Api 출처 데이터는 덮어씀).</summary>
    public void ReplaceYearFromApi(int year, IReadOnlyList<Holiday> holidays)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Holiday WHERE Source = 'Api' AND Date LIKE $yearPrefix;";
            delete.Parameters.AddWithValue("$yearPrefix", $"{year:D4}-%");
            delete.ExecuteNonQuery();
        }

        foreach (var holiday in holidays)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO Holiday (Date, Name, Kind, Source) VALUES ($date, $name, $kind, $source)
                ON CONFLICT(Date) DO UPDATE SET Name = excluded.Name, Kind = excluded.Kind, Source = excluded.Source;
                """;
            BindParameters(insert, holiday);
            insert.ExecuteNonQuery();
        }

        using (var markCached = connection.CreateCommand())
        {
            markCached.Transaction = transaction;
            markCached.CommandText = "INSERT OR IGNORE INTO HolidayCachedYear (Year) VALUES ($year);";
            markCached.Parameters.AddWithValue("$year", year);
            markCached.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyList<Holiday> GetByYear(int year) => Query("Date LIKE $pattern", ("$pattern", $"{year:D4}-%"));

    public IReadOnlyList<Holiday> GetByMonth(int year, int month) =>
        Query("Date LIKE $pattern", ("$pattern", $"{year:D4}-{month:D2}-%"));

    public Holiday? GetByDate(DateOnly date) =>
        Query("Date = $date", ("$date", date.ToString("yyyy-MM-dd"))).FirstOrDefault();

    public void AddManual(DateOnly date, string name)
    {
        var holiday = new Holiday { Date = date, Name = name, Kind = HolidayKind.Manual, Source = HolidaySource.Manual };
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Holiday (Date, Name, Kind, Source) VALUES ($date, $name, $kind, $source)
            ON CONFLICT(Date) DO UPDATE SET Name = excluded.Name, Kind = excluded.Kind, Source = excluded.Source;
            """;
        BindParameters(command, holiday);
        command.ExecuteNonQuery();
    }

    /// <summary>해당 날짜의 공휴일 지정을 해제한다 (API 출처든 수동 출처든 모두 제거).</summary>
    public void Remove(DateOnly date)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Holiday WHERE Date = $date;";
        command.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        command.ExecuteNonQuery();
    }

    private List<Holiday> Query(string whereClause, params (string Name, object Value)[] parameters)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Date, Name, Kind, Source FROM Holiday WHERE {whereClause};";
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        var results = new List<Holiday>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new Holiday
            {
                Date = DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Name = reader.GetString(1),
                Kind = Enum.Parse<HolidayKind>(reader.GetString(2)),
                Source = Enum.Parse<HolidaySource>(reader.GetString(3)),
            });
        }

        return results;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void BindParameters(SqliteCommand command, Holiday holiday)
    {
        command.Parameters.AddWithValue("$date", holiday.Date.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$name", holiday.Name);
        command.Parameters.AddWithValue("$kind", holiday.Kind.ToString());
        command.Parameters.AddWithValue("$source", holiday.Source.ToString());
    }
}
