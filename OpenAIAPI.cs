using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

public class OpenAIApi
{
    private readonly HttpClient httpClient = new HttpClient();
    private const string ApiEndpoint = "https://api.openai.com/v1/chat/completions";

    private readonly string openAIKey = Environment.GetEnvironmentVariable("OPENAI_KEY");
    private readonly string openAIOrg = Environment.GetEnvironmentVariable("OPENAI_ORG");
    private readonly string modelId = Environment.GetEnvironmentVariable("MODEL_ID");
    private readonly string assistantName = Environment.GetEnvironmentVariable("ASSISTANT_NAME");

    public async Task<OpenAIApiResponse> SendRequestAsync(string initialPrompString, IEnumerable<MeaningfulChunk> chunks)
    {
        var next = new ChatCompletionRequest
        {
            Model = modelId,
            Messages = CreateMessages(initialPrompString, chunks),
            Temperature = 0.7
        };

        var requestJson = JsonConvert.SerializeObject(next);
        var requestContent = new StringContent(
            requestJson,
            System.Text.Encoding.UTF8,
            "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint)
        {
            Content = requestContent,
            Headers = {
                { "Authorization", $"Bearer {openAIKey}" },
                { "OpenAI-Organization", openAIOrg }
            }
        };

        try
        {
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode == false)
                Console.WriteLine($"Response - {response.StatusCode} {response.ReasonPhrase}");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var responseContentObject = JsonConvert.DeserializeObject<OpenAIApiResponse>(responseContent);
            Console.WriteLine($"[{assistantName}] {responseContentObject.Choices[0].Message.Content}");
            return responseContentObject;
        }
        catch
        {
            Console.WriteLine($"OpenAI request failed: {requestJson}");
            return null;
        }
    }

    public List<Message> CreateMessages(string initialPrompString, IEnumerable<MeaningfulChunk> chunks)
    {
        var messages = new List<Message>();
        messages.Add(new Message
        {
            Role = Role.System,
            Content = initialPrompString
        });

        foreach (var chunk in chunks)
        {
            // Create a Message for the speech recognition result
            if (chunk.RecognitionEvent != null && chunk.RecognitionEvent.Result != null)
            {
                var userMessage = new Message
                {
                    Content = chunk.RecognitionEvent.Result.Text,
                    Role = Role.User
                };
                messages.Add(userMessage);
            }

            // Create a Message for the OpenAI API response
            if (chunk.OpenAITask != null && chunk.OpenAITask.Result != null)
            {
                var assistantMessage = new Message
                {
                    Content = chunk.OpenAITask.Result.Choices[0].Message.Content,
                    Role = Role.Assistant
                };
                messages.Add(assistantMessage);
            }
        }

        return messages;
    }
}

public class OpenAIApiResponse
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("created")]
    public long Created { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("usage")]
    public Usage Usage { get; set; }

    [JsonProperty("choices")]
    public List<Choice> Choices { get; set; }
}

public class Usage
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}

public class Message
{
    [JsonProperty("content", Required = Required.Always)]
    public string Content { get; set; }  // Can be null according to documentation

    /*[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    [MaxLength(64, ErrorMessage = "Name length can't exceed 64 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_]*$", ErrorMessage = "Name can only contain a-z, A-Z, 0-9, and underscores.")]
    public string Name { get; set; }  // Optional field*/

    [JsonProperty("role", Required = Required.Always)]
    [EnumDataType(typeof(Role), ErrorMessage = "Invalid role.")]
    public Role Role { get; set; }  // Enum type to enforce valid values
}

[JsonConverter(typeof(StringEnumConverter))]
public enum Role
{
    [EnumMember(Value = "system")]
    System,
    [EnumMember(Value = "user")]
    User,
    [EnumMember(Value = "assistant")]
    Assistant,
    [EnumMember(Value = "function")]
    Function
}


public class Choice
{
    [JsonProperty("message")]
    public Message Message { get; set; }

    [JsonProperty("finish_reason")]
    public string FinishReason { get; set; }

    [JsonProperty("index")]
    public int Index { get; set; }
}

public class ChatCompletionRequest
{
    [JsonProperty("messages", Required = Required.Always)]
    public List<Message> Messages { get; set; } = new List<Message>();

    [JsonProperty("model", Required = Required.Always)]
    public string Model { get; set; }

    [JsonProperty("frequency_penalty", NullValueHandling = NullValueHandling.Ignore)]
    public double? FrequencyPenalty { get; set; }

    [JsonProperty("function_call", NullValueHandling = NullValueHandling.Ignore)]
    public dynamic FunctionCall { get; set; }  // Could be string or object

    [JsonProperty("functions", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Functions { get; set; } = new List<string>();

    [JsonProperty("logit_bias", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, double> LogitBias { get; set; } = new Dictionary<string, double>();

    [JsonProperty("max_tokens", NullValueHandling = NullValueHandling.Ignore)]
    public int? MaxTokens { get; set; }

    [JsonProperty("n", NullValueHandling = NullValueHandling.Ignore)]
    public int? N { get; set; }

    [JsonProperty("presence_penalty", NullValueHandling = NullValueHandling.Ignore)]
    public double? PresencePenalty { get; set; }

    [JsonProperty("stop", NullValueHandling = NullValueHandling.Ignore)]
    public dynamic Stop { get; set; }  // Could be string, array or null

    [JsonProperty("stream", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Stream { get; set; }

    [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
    public double? Temperature { get; set; }

    [JsonProperty("top_p", NullValueHandling = NullValueHandling.Ignore)]
    public double? TopP { get; set; }

    [JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
    public string User { get; set; }

    public bool ShouldSerializeFunctions()
    {
        return Functions != null && Functions.Count > 0;
    }
}