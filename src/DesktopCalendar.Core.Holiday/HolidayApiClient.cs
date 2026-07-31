using System.Globalization;
using System.Text.Json;

namespace DesktopCalendar.Core.Holiday;

/// <summary>
/// 공공데이터포털 "한국천문연구원_특일 정보" API 클라이언트 (DESIGN.md 4.2).
/// 서비스 키는 data.go.kr에서 사용자가 직접 발급받은 "Decoding" 키를 그대로 넘기면 된다
/// (이 클래스에서 URL 인코딩을 처리한다).
/// </summary>
public sealed class HolidayApiClient(string serviceKey, HttpClient? httpClient = null)
{
    private const string BaseUrl = "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getHoliDeInfo";

    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    public async Task<IReadOnlyList<Holiday>> GetHolidaysAsync(int year, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?serviceKey={Uri.EscapeDataString(serviceKey)}" +
                   $"&solYear={year}&numOfRows=100&_type=json";

        string body;
        try
        {
            body = await _httpClient.GetStringAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new HolidayApiException($"공휴일 API 호출에 실패했습니다: {ex.Message}");
        }

        return ParseResponse(body, year);
    }

    private static List<Holiday> ParseResponse(string json, int year)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("response", out var response))
            throw new HolidayApiException($"공휴일 API 응답 형식이 올바르지 않습니다: {Truncate(json)}");

        var header = response.GetProperty("header");
        var resultCode = header.GetProperty("resultCode").GetString();
        if (resultCode != "00")
        {
            var resultMsg = header.TryGetProperty("resultMsg", out var msgEl) ? msgEl.GetString() : "알 수 없는 오류";
            throw new HolidayApiException($"공휴일 API 오류 ({resultCode}): {resultMsg}. API 키가 올바른지 확인하세요.");
        }

        var body = response.GetProperty("body");
        if (!body.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Object)
            return []; // 해당 연도에 데이터가 없음 (items가 빈 문자열로 오는 경우 포함)

        if (!items.TryGetProperty("item", out var itemEl))
            return [];

        var results = new List<Holiday>();
        foreach (var item in EnumerateItems(itemEl))
        {
            var isHoliday = item.TryGetProperty("isHoliday", out var isHolidayEl) && isHolidayEl.GetString() == "Y";
            if (!isHoliday)
                continue;

            var locdate = item.GetProperty("locdate").GetInt32().ToString(CultureInfo.InvariantCulture);
            var date = DateOnly.ParseExact(locdate, "yyyyMMdd", CultureInfo.InvariantCulture);
            var name = item.GetProperty("dateName").GetString() ?? "공휴일";

            results.Add(new Holiday
            {
                Date = date,
                Name = name,
                Kind = ClassifyKind(name),
                Source = HolidaySource.Api,
            });
        }

        return results;
    }

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement itemEl) =>
        itemEl.ValueKind == JsonValueKind.Array ? itemEl.EnumerateArray() : [itemEl];

    private static HolidayKind ClassifyKind(string name)
    {
        if (name.Contains("임시공휴일"))
            return HolidayKind.TemporaryHoliday;
        if (name.Contains("대체"))
            return HolidayKind.SubstituteHoliday;
        return HolidayKind.PublicHoliday;
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "...";
}
