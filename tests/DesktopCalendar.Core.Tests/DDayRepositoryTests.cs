using Microsoft.Data.Sqlite;
using DesktopCalendar.Core.Calendar;
using Xunit;

namespace DesktopCalendar.Core.Tests;

/// <summary>DDay 저장소의 스키마 마이그레이션과 왕복 저장 검증 (2026-08-31 기준일 방식 추가 시 신설).</summary>
public class DDayRepositoryTests
{
    [Fact]
    public void OldSchemaDb_IsMigrated_AndRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ddaymig_{Guid.NewGuid():N}.db");
        try
        {
            // 예전 버전이 만들던 4열짜리 DDay 테이블 + 기존 데이터 1건
            using (var seed = new SqliteConnection($"Data Source={path}"))
            {
                seed.Open();
                using var cmd = seed.CreateCommand();
                cmd.CommandText =
                    """
                    CREATE TABLE DDay (
                        Id TEXT PRIMARY KEY NOT NULL, Title TEXT NOT NULL,
                        TargetDate TEXT NOT NULL, IsRecurringYearly INTEGER NOT NULL);
                    INSERT INTO DDay VALUES ('11111111-1111-1111-1111-111111111111', '기존항목', '2026-12-25', 1);
                    """;
                cmd.ExecuteNonQuery();
            }

            var repo = new DDayRepository(path);

            // 1) 기존 항목이 살아있고 기준일 정보는 비어 있다
            var existing = Assert.Single(repo.GetAll());
            Assert.Equal("기존항목", existing.Title);
            Assert.Equal(new DateOnly(2026, 12, 25), existing.TargetDate);
            Assert.True(existing.IsRecurringYearly);
            Assert.False(existing.IsOffsetBased);

            // 2) 기준일 방식 항목 저장 → 왕복
            var baseDate = new DateOnly(2026, 9, 1);
            repo.Add(new DDay
            {
                Title = "100일",
                TargetDate = DDayCalculator.ComputeTargetFromBase(baseDate, 100),
                BaseDate = baseDate,
                OffsetDays = 100,
            });

            var saved = repo.GetAll().Single(d => d.Title == "100일");
            Assert.True(saved.IsOffsetBased);
            Assert.Equal(new DateOnly(2026, 12, 10), saved.TargetDate);
            Assert.Equal(baseDate, saved.BaseDate);
            Assert.Equal(100, saved.OffsetDays);

            // 3) 기준일 방식 → 직접 지정으로 수정하면 두 열이 NULL로 비워진다
            saved.BaseDate = null;
            saved.OffsetDays = null;
            saved.TargetDate = new DateOnly(2027, 1, 1);
            repo.Update(saved);

            var reverted = repo.GetAll().Single(d => d.Title == "100일");
            Assert.False(reverted.IsOffsetBased);
            Assert.Null(reverted.BaseDate);
            Assert.Null(reverted.OffsetDays);
            Assert.Equal(new DateOnly(2027, 1, 1), reverted.TargetDate);

            // 4) 두 번째 생성(EnsureSchema 재실행)에서 ALTER가 중복 실행되지 않는다
            var repo2 = new DDayRepository(path);
            Assert.Equal(2, repo2.GetAll().Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
