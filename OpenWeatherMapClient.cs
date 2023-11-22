using System;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class OpenWeatherMapClient
{
    public Tool GetCurrentLocalWeatherTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "get_current_local_weather",
            Description = "Returns current local weather data from Open Weather Map API."
        }
    };

    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private Func<Tuple<double, double>> locationProvider; 

    public OpenWeatherMapClient(string apiKey, Func<Tuple<double, double>> locationProvider)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        GetCurrentLocalWeatherTool.Execute = GetCurrentLocalWeatherAsync;
        this.locationProvider = locationProvider;
    }

    public async Task<string> GetWeatherAsync(CancellationToken cancelToken)
    {
        var location = locationProvider.Invoke();
        string url = $"https://api.openweathermap.org/data/2.5/weather?lat={location.Item1}&lon={location.Item2}&appid={_apiKey}";
        var response = await _httpClient.GetAsync(url, cancelToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancelToken);
    }

    public async Task<Message> GetCurrentLocalWeatherAsync(ToolCall toolCall, CancellationToken cancelToken)
    {
        var responseBody = await GetWeatherAsync(cancelToken);
        return new Message {
            Content = $"OpenWeatherMap current weather report:\n{responseBody}\nThe Client prefers fahrenheit units.",
            Role = Role.Tool,
            ToolCallId = toolCall.Id,
            FollowUp = true
        };
    }
}

public class WeatherResponse
{
    // Define properties here based on the JSON structure returned by the API
    // For example:
    public string Name { get; set; }
    // Add other properties as needed
}
