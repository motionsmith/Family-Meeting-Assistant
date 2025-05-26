public class NewsMessageProvider : IMessageProvider
{
    public Tool GetTopHeadlinesTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "get_top_headlines",
            Description = "Returns top news headlines from NewsAPI for the US."
        }
    };

    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private DateTime lastReport = DateTime.UnixEpoch;

    public NewsMessageProvider(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
        // Identify this application via User-Agent as required by NewsAPI
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "FamilyMeetingAssistant/1.0");
        GetTopHeadlinesTool.Execute = GetTopHeadlinesAsync;
    }

    public async Task<string> GetHeadlinesAsync(CancellationToken cancelToken)
    {
        var url = $"https://newsapi.org/v2/top-headlines?country=us&apiKey={_apiKey}";
        var response = await _httpClient.GetAsync(url, cancelToken);
        var content = await response.Content.ReadAsStringAsync(cancelToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Request to {url} failed with status code {(int)response.StatusCode} ({response.StatusCode}). Response body: {content}");
        }
        return content;
    }

    public async Task<Message> GetTopHeadlinesAsync(ToolCall toolCall, CancellationToken cancelToken)
    {
        // Handle missing API key
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new Message
            {
                Content = "News API key not configured; cannot fetch headlines.",
                Role = Role.Tool,
                ToolCallId = toolCall.Id,
                FollowUp = false
            };
        }
        try
        {
            var responseBody = await GetHeadlinesAsync(cancelToken);
            return new Message
            {
                Content = $"### Top headlines on demand:\n\n{responseBody}\n",
                Role = Role.Tool,
                ToolCallId = toolCall.Id,
                FollowUp = true
            };
        }
        catch (Exception ex)
        {
            return new Message
            {
                Content = $"[News] failed on-demand headline fetch: {ex.Message}",
                Role = Role.Tool,
                ToolCallId = toolCall.Id,
                FollowUp = false
            };
        }
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var messages = new List<Message>();
        // Skip periodic updates if no API key configured
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return messages;
        }
        var now = DateTime.Now;
        var duration = now - lastReport;
        var thres = TimeSpan.FromMinutes(60);
        if (duration > thres)
        {
            try
            {
                var reportContent = await GetHeadlinesAsync(cts.Token);
                messages.Add(new Message
                {
                    Role = Role.System,
                    Content = $"### Hourly system headlines\n\n{reportContent}\n"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[News] periodic update failed: {ex.Message}");
            }
            finally
            {
                lastReport = now;
            }
        }
        return messages;
    }
}