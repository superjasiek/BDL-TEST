using System.Net.Http;
using System.Text.Json;

namespace BdlGusExporter.Core
{
    public class GusApiService
    {
        private const string ApiBase = "https://bdl.stat.gov.pl/api/v1";
        private readonly HttpClient _httpClient = new HttpClient();

        public void SetApiKey(string apiKey)
        {
            if (_httpClient.DefaultRequestHeaders.Contains("X-ClientId"))
                _httpClient.DefaultRequestHeaders.Remove("X-ClientId");

            if (!string.IsNullOrEmpty(apiKey))
                _httpClient.DefaultRequestHeaders.Add("X-ClientId", apiKey);
        }

        public async Task<JsonDocument> GetUnitsAsync(string? parentId = null, int? level = null)
        {
            var queryParams = new List<string>();
            if (parentId != null) queryParams.Add($"parent-id={parentId}");

            // Default to level 0 if no parentId is provided
            if (level == null && parentId == null) queryParams.Add("level=0");
            else if (level != null) queryParams.Add($"level={level}");

            queryParams.Add("format=json");
            queryParams.Add("page-size=1000"); // Increased page size

            var url = $"{ApiBase}/units?{string.Join("&", queryParams)}";
            var json = await _httpClient.GetStringAsync(url);
            return JsonDocument.Parse(json);
        }

        public async Task<JsonDocument> GetDataForUnitAsync(string unitId, string varId, IEnumerable<int> years)
        {
            var queryYears = string.Join("&", years.Select(y => $"year={y}"));
            var url = $"{ApiBase}/data/by-unit/{unitId}?var-id={varId}&{queryYears}&format=json";
            var json = await _httpClient.GetStringAsync(url);
            return JsonDocument.Parse(json);
        }
    }
}
