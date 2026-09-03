using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows_10_MenuBar.Models;
using Wpf.Ui.Common;

namespace Windows_10_MenuBar.Services;

public class WeatherResult
{
    public string Condition { get; init; } = string.Empty;
    public string Tooltip   { get; init; } = string.Empty;
    public SymbolRegular Icon { get; init; } = SymbolRegular.WeatherSunny24;
}

public class WeatherService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private List<CityData> _cities = new();
    private string _province = string.Empty;
    private string _district = string.Empty;

    // ── City data ────────────────────────────────────────────────────────────

    public IReadOnlyList<CityData> Cities => _cities;

    public void LoadCities()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "cities.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            _cities = JsonSerializer.Deserialize<List<CityData>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch { }
    }

    public IEnumerable<string> GetDistrictsFor(string province)
    {
        var city = _cities.FirstOrDefault(c =>
            string.Equals(c.name, province, StringComparison.OrdinalIgnoreCase));
        return city?.counties?
            .Select(c => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(c))
            ?? Enumerable.Empty<string>();
    }

    // ── Location settings ────────────────────────────────────────────────────

    public void SetLocation(string province, string district)
    {
        _province = province;
        _district = district;
    }

    // ── Weather fetch ────────────────────────────────────────────────────────

    public async Task<WeatherResult> FetchAsync()
    {
        try
        {
            double lat, lon;
            string city;

            if (string.IsNullOrWhiteSpace(_province))
            {
                (lat, lon, city) = await GetLocationFromIpAsync();
            }
            else
            {
                var nominatim = await TryGetLocationFromNominatimAsync();
                if (nominatim.HasValue)
                {
                    (lat, lon, city) = nominatim.Value;
                }
                else
                {
                    var local = TryGetLocationFromCitiesJson();
                    if (local.HasValue) (lat, lon, city) = local.Value;
                    else return new WeatherResult { Condition = "Bulunamadı", Tooltip = $"{_province} bulunamadı." };
                }
            }

            return await FetchOpenMeteoAsync(lat, lon, city);
        }
        catch
        {
            return new WeatherResult { Condition = "Hata", Tooltip = "Hava durumu alınamadı." };
        }
    }

    public async Task RunLoopAsync(Func<WeatherResult, Task> onUpdate, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await FetchAsync();
                await onUpdate(result);
            }
            catch (OperationCanceledException) { break; }
            catch { }

            try { await Task.Delay(TimeSpan.FromMinutes(30), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static async Task<(double lat, double lon, string city)> GetLocationFromIpAsync()
    {
        var json = await _http.GetStringAsync("http://ip-api.com/json/");
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        double lat = data.TryGetProperty("lat", out var lp) ? lp.GetDouble() : 0;
        double lon = data.TryGetProperty("lon", out var np) ? np.GetDouble() : 0;
        string city = data.TryGetProperty("city", out var cp) ? cp.GetString() ?? "Şehir" : "Şehir";
        return (lat, lon, city);
    }

    private async Task<(double lat, double lon, string city)?> TryGetLocationFromNominatimAsync()
    {
        try
        {
            string query = string.IsNullOrWhiteSpace(_district)
                ? $"{_province}, Turkey"
                : $"{_district}, {_province}, Turkey";

            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1");
            req.Headers.Add("User-Agent", "Windows10MenuBarApp/1.0");

            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<JsonElement>(json);

            if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return null;

            var first = results[0];
            if (!first.TryGetProperty("lat", out var lp) || !first.TryGetProperty("lon", out var np)) return null;

            double lat = double.Parse(lp.GetString() ?? "0", CultureInfo.InvariantCulture);
            double lon = double.Parse(np.GetString() ?? "0", CultureInfo.InvariantCulture);
            string city = string.IsNullOrWhiteSpace(_district) ? _province : _district;
            return (lat, lon, city);
        }
        catch { return null; }
    }

    private (double lat, double lon, string city)? TryGetLocationFromCitiesJson()
    {
        var known = _cities.FirstOrDefault(c =>
            string.Equals(c.name, _province, StringComparison.OrdinalIgnoreCase));
        if (known == null || string.IsNullOrEmpty(known.latitude)) return null;

        double lat = double.Parse(known.latitude, CultureInfo.InvariantCulture);
        double lon = double.Parse(known.longitude, CultureInfo.InvariantCulture);
        string city = string.IsNullOrWhiteSpace(_district) ? _province : _district;
        return (lat, lon, city);
    }

    private static async Task<WeatherResult> FetchOpenMeteoAsync(double lat, double lon, string city)
    {
        string url = $"https://api.open-meteo.com/v1/forecast" +
                     $"?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
                     $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                     $"&current_weather=true";

        var json = await _http.GetStringAsync(url);
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        if (!data.TryGetProperty("current_weather", out var cw))
            return new WeatherResult { Condition = "?", Tooltip = city };

        double temp = cw.GetProperty("temperature").GetDouble();
        int code    = cw.GetProperty("weathercode").GetInt32();

        return new WeatherResult
        {
            Condition = $"{Math.Round(temp)}°C",
            Tooltip   = $"{city} - Hava Durumu",
            Icon      = CodeToIcon(code),
        };
    }

    public static SymbolRegular CodeToIcon(int code) => code switch
    {
        0          => SymbolRegular.WeatherSunny24,
        1 or 2     => SymbolRegular.WeatherPartlyCloudyDay24,
        3          => SymbolRegular.WeatherCloudy24,
        >= 45 and <= 48 => SymbolRegular.WeatherFog24,
        >= 51 and <= 67 => SymbolRegular.WeatherRainShowersDay24,
        >= 71 and <= 82 => SymbolRegular.WeatherSnowflake24,
        >= 95      => SymbolRegular.WeatherThunderstorm24,
        _          => SymbolRegular.WeatherSunny24,
    };
}
