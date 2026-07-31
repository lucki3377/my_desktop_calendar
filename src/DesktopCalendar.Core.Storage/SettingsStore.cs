using Microsoft.Data.Sqlite;

namespace DesktopCalendar.Core.Storage;

/// <summary>
/// 단순 key-value 형태의 앱 설정 저장소 (SQLite 기반, DESIGN.md 4.8).
/// 위젯 위치/크기, 표시 토글 등 모든 설정을 문자열로 저장한다.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _connectionString;

    public SettingsStore(string? dbPath = null)
    {
        dbPath ??= AppPaths.DatabaseFilePath;
        _connectionString = $"Data Source={dbPath}";
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Settings (
                Key   TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public string? GetString(string key, string? defaultValue = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);

        var result = command.ExecuteScalar();
        return result is null or DBNull ? defaultValue : (string)result;
    }

    public void SetString(string key, string value)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public double GetDouble(string key, double defaultValue)
    {
        var raw = GetString(key);
        return raw is not null && double.TryParse(raw, out var value) ? value : defaultValue;
    }

    public void SetDouble(string key, double value) => SetString(key, value.ToString("R"));

    public bool GetBool(string key, bool defaultValue)
    {
        var raw = GetString(key);
        return raw is not null && bool.TryParse(raw, out var value) ? value : defaultValue;
    }

    public void SetBool(string key, bool value) => SetString(key, value.ToString());
}
