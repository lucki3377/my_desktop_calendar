using DesktopCalendar.Core.Storage;
using Google.Apis.Json;
using Google.Apis.Util.Store;
using Microsoft.Data.Sqlite;

namespace DesktopCalendar.Core.Google;

/// <summary>
/// Google API 라이브러리가 토큰을 보관할 때 쓰는 저장소(<see cref="IDataStore"/>) 구현.
/// 값을 DPAPI로 암호화해서 SQLite에 넣는다 (DESIGN.md 4.4 — 라이브러리 기본 FileDataStore는 평문 JSON이라 사용하지 않음).
/// </summary>
public sealed class SqliteDpapiDataStore : IDataStore
{
    private readonly string _connectionString;

    public SqliteDpapiDataStore(string? dbPath = null)
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
            CREATE TABLE IF NOT EXISTS GoogleToken (
                Key   TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public Task StoreAsync<T>(string key, T value)
    {
        var json = NewtonsoftJsonSerializer.Instance.Serialize(value);

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO GoogleToken (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", ComposeKey<T>(key));
        command.Parameters.AddWithValue("$value", TokenProtector.ProtectToBase64(json));
        command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM GoogleToken WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", ComposeKey<T>(key));

        var stored = command.ExecuteScalar() as string;
        var json = TokenProtector.UnprotectFromBase64(stored);

        return Task.FromResult(json is null
            ? default!
            : NewtonsoftJsonSerializer.Instance.Deserialize<T>(json));
    }

    public Task DeleteAsync<T>(string key)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GoogleToken WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", ComposeKey<T>(key));
        command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GoogleToken;";
        command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    /// <summary>FileDataStore와 동일하게 타입 이름을 키에 섞어 서로 다른 타입의 키가 충돌하지 않게 한다.</summary>
    private static string ComposeKey<T>(string key) => $"{typeof(T).FullName}-{key}";

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
