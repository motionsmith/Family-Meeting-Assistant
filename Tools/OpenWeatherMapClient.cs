using System;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class WeatherMessageProvider : IMessageProvider
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
    private DateTime lastReport = DateTime.UnixEpoch;

    public WeatherMessageProvider(string apiKey, Func<Tuple<double, double>> locationProvider)
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
            Content = $"### Weather update on demand:\n\n{responseBody}\nThe Client prefers fahrenheit units. You follow up.",
            Role = Role.Tool,
            ToolCallId = toolCall.Id,
            FollowUp = true
        };
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var messages = new List<Message>();
        var now = DateTime.Now;
        var duration = now - lastReport;
        var thres = TimeSpan.FromMinutes(60);
        if (duration > thres)
        {
            var reportContent = await GetWeatherAsync(cts.Token);
            messages.Add(new Message
            {
                Role = Role.System,
                Content = $"### Hourly system weather update\n\n{reportContent}\nThe Client prefers fahrenheit units.\nYou remain silent \"...\" unless this update is significant."
            });
            lastReport = now;
        }
        
        return messages.AsEnumerable();
    }
}

public class WeatherResponse
{
    // Define properties here based on the JSON structure returned by the API
    // For example:
    public string Name { get; set; }
    // Add other properties as needed
}
