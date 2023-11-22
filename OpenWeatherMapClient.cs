using System;
using System.Net.Http;
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

    public OpenWeatherMapClient(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        GetCurrentLocalWeatherTool.Execute = GetCurrentLocalWeatherAsync;
    }

    public async Task<string> GetWeatherAsync(double latitude, double longitude, CancellationToken cancelToken)
    {
        string url = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={_apiKey}";
        var response = await _httpClient.GetAsync(url, cancelToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancelToken);
    }

    public async Task<Message> GetCurrentLocalWeatherAsync(ToolCall toolCall, CancellationToken cancelToken)
    {
        var jArgs = JObject.Parse(toolCall.Function.Arguments);
        var latitude = (float)jArgs["lat"];
        var longitude = (float)jArgs["long"];
        var responseBody = await GetWeatherAsync(latitude, longitude, cancelToken);
        return new Message {
            Content = $"{responseBody}\nThe client prefers fahrenheit units.",
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
