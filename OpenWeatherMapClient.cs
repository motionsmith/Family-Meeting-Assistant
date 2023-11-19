using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class OpenWeatherMapClient
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public OpenWeatherMapClient(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
    }

    public async Task<string> GetWeatherAsync(double latitude, double longitude, CancellationToken cancelToken)
    {
        string url = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={_apiKey}";
        var response = await _httpClient.GetAsync(url, cancelToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancelToken);
    }

    public async Task<Message> GetWeatherAsync(ToolCall toolCall, double latitude, double longitude, CancellationToken cancelToken)
    {
        var responseBody = await GetWeatherAsync(latitude, longitude, cancelToken);
        return new Message {
            Content = responseBody,
            Role = Role.Tool,
            ToolCallId = toolCall.Id
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
