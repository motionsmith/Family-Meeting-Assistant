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

    private readonly string chatCompletionPrompt;
    private readonly string toolCallPrompt;

    public List<Tool> Tools = new List<Tool>();
    
    public OpenAIApi()
    {
        httpClient.Timeout = TimeSpan.FromSeconds(15);
        
        chatCompletionPrompt = "\n\n## [[ASSISTANT_NAME]]s instructions for speaking\n\n";
        chatCompletionPrompt += "\nYour message will cause the text content to be read aloud via text-to-speech over the laptop speakers so that The Client can hear you.";
        chatCompletionPrompt += "\nYour speaking style sounds like it was meant to be heard, not read.";
        chatCompletionPrompt += "\nWhen you speak, it will feel delayed to us due to network latency.";
        chatCompletionPrompt += "\nWhen you speak, your text is spoken slowly and somewhat robotically, so keep your spoken text brief.";
        chatCompletionPrompt += "\nSince you can only read the transcription, you can only use intuition to figure out who is speaking. Feel free to ask for clarification, but only when necessary, as this is an interruption.";
        chatCompletionPrompt += "\nWhen speaking, be straightforward, not overly nice.";
        chatCompletionPrompt += "\nDo not bother to tell us that you are available to help because we already know you're here.";

        toolCallPrompt = "\n\n## [[ASSISTANT_NAME]]'s instructions for function calling\n\n";
        toolCallPrompt += "\nYou always output JSON to call functions.";
        toolCallPrompt += "\nThe JSON you output will be interpreted by the client and a function will be executed on your behalf.";

        toolCallPrompt += "## [[ASSISTANT_NAME]]s instructions for speaking\n\n";
        toolCallPrompt += "\nYour speaking style sounds like it was meant to be heard, not read.";
        toolCallPrompt += "\nIf you must compose, add your text to your message content. This will cause the text content to be read (via text-to-speech) over the laptop speakers so that The Client can hear you.";
        toolCallPrompt += "\nYou do not address people before they address you, unless you are speaking for some other approved reason.";
        toolCallPrompt += "\nYou proactively reminds Clients of tasks due soon without being prompted.";
        toolCallPrompt += "\nYou do not discuss tasks that are not due soon unless The Client directly inquires about one.";
        toolCallPrompt += "\nYou speak a response when someone addresses you as [[ASSISTANT_NAME]], but you are brief.";
        toolCallPrompt += "\nWhen you speak, it will feel delayed to us due to network latency.";
        toolCallPrompt += "\nWhen you speak, your text is spoken slowly and somewhat robotically, so keep your spoken text brief.";
        toolCallPrompt += "\nIf someone thanks you, do not respond.";
        toolCallPrompt += "\nThe Client does not want to hear from you too often or it will feel intrusive.";
        toolCallPrompt += "\nSince you can only read the transcription, you can only use intuition to figure out who is speaking. Feel free to ask for clarification, but only when necessary, as this is an interruption.";
        toolCallPrompt += "\nIf someone asks you a question, such as \"Hey [[ASSISTANT_NAME]], what are our current action items?\", then you may speak a response.";
        toolCallPrompt += "\nWhen speaking, be straightforward, not overly nice. You do not bother with passive comments like \"If you need anything, just let me know.\" or \"Is there anything else I can help you with?\"";
    }

    private async Task<OpenAIApiResponse> CompleteChatAsync(IEnumerable<Message> messages, CancellationToken cancelToken, ResponseFormat? responseFormat = null, List<Tool>? tools = null)
    {
        var next = new ChatCompletionRequest
        {
            Model = modelId,
            Messages = messages.ToList(),
            Temperature = 0.7,
            Tools = tools,
            ResponseFormat = null
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
        
        var response = await httpClient.SendAsync(request, cancelToken);
        if (response.IsSuccessStatusCode == false)
        {
            var failureResponseContent = await response.Content.ReadAsStringAsync();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"OPENAI ERROR - {response.StatusCode} {response.ReasonPhrase} {failureResponseContent}");
            Console.WriteLine($"REQUEST DUMP:\n\n{requestJson}");
            Console.ResetColor();
        }
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseContentObject = JsonConvert.DeserializeObject<OpenAIApiResponse>(responseContent);

        /*if (response.IsSuccessStatusCode && next.Tools != null && next.Tools.Count > 0 && responseContentObject.Choices[0].FinishReason != FinishReason.ToolCalls)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Malformed tool call from OPENAI. Response dump is below.");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(responseContent);
            Console.ResetColor();
        }*/
        return responseContentObject;
    }

    public async Task<Message> GetToolCallAsync(List<Message> messages, CancellationToken tkn)
    {
        messages[0].Content += toolCallPrompt;
        var openAiResponse = await CompleteChatAsync(messages, tkn, null, Tools);

        /*foreach (var choice in openAiResponse.Choices)
        {
            if (choice.FinishReason != FinishReason.ToolCalls)
            {
                Console.WriteLine($"DEBUG WARNING: Tool call finish reason {choice.FinishReason}");
            }
        }*/
        if (openAiResponse.Error == null)
        {
            return openAiResponse.Choices[0].Message;
        }
        return new Message {
            Role = Role.System,
            Content = $"Error from OpenAI API: {openAiResponse.Error.Message}"
        };
    }

    public async Task<Message> GetChatCompletionAsync(List<Message> messages, CancellationToken tkn)
    {
        messages[0].Content += chatCompletionPrompt;
        var openAiResponse = await CompleteChatAsync(messages, tkn, null, null);
        if (openAiResponse.Error == null)
        {
            return openAiResponse.Choices[0].Message;
        }
        return new Message {
            Role = Role.System,
            Content = $"Error from OpenAI API: {openAiResponse.Error.Message}"
        };
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

    [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
    public OpenAiError? Error { get; set; }
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
    [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
    public string Content { get; set; }  // Can be null according to documentation

    [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
    public List<ToolCall>? ToolCalls { get; set; }

    [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? ToolCallId { get; set; }

    [JsonProperty("role", Required = Required.Always)]
    [EnumDataType(typeof(Role), ErrorMessage = "Invalid role.")]
    public Role Role { get; set; }  // Enum type to enforce valid values

    [JsonIgnore]
    public bool FollowUp { get; set; }
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
    [EnumMember(Value = "tool")]
    Tool
}

[JsonConverter(typeof(StringEnumConverter))]
public enum FinishReason
{
    [EnumMember(Value = "tool_calls")]
    ToolCalls,
    [EnumMember(Value = "length")]
    Length,
    [EnumMember(Value = "stop")]
    Stop
}

public class Choice
{
    [JsonProperty("message")]
    public Message Message { get; set; }

    [JsonProperty("finish_reason")]
    public FinishReason FinishReason { get; set; }

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

    [JsonProperty("tool_choice", NullValueHandling = NullValueHandling.Ignore)]
    public List<Tool>? ToolChoice { get; set; }

    [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
    public List<Tool>? Tools { get; set; }

    [JsonProperty("logit_bias", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, double> LogitBias { get; set; } = new Dictionary<string, double>();

    [JsonProperty("max_tokens", NullValueHandling = NullValueHandling.Ignore)]
    public int? MaxTokens { get; set; }

    [JsonProperty("n", NullValueHandling = NullValueHandling.Ignore)]
    public int? N { get; set; }

    [JsonProperty("presence_penalty", NullValueHandling = NullValueHandling.Ignore)]
    public double? PresencePenalty { get; set; }

    [JsonProperty("response_format", NullValueHandling = NullValueHandling.Ignore)]
    public ResponseFormat? ResponseFormat { get; set; }

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

    public bool ShouldSerializeTools()
    {
        return Tools != null && Tools.Count > 0;
    }

    public bool ShouldSerializeToolChoice()
    {
        return ToolChoice != null && ToolChoice.Count > 0;
    }
}

public class ToolFunctionParameterProperty
{
    [JsonProperty("type", Required = Required.Always)]
    public string Type { get; set; }

    [JsonProperty("description", Required = Required.Always)]
    public string Description { get; set; }

    [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Enum { get; set; }
}

public class ToolFunctionParameters
{
    [JsonProperty("type")]
    public string Type = "object";

    [JsonProperty("properties")]
    public Dictionary<string, ToolFunctionParameterProperty> Properties { get; set; } = new Dictionary<string, ToolFunctionParameterProperty>();

    [JsonProperty("required", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Required { get; set; }
}

public class ToolFunction
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("parameters", Required = Required.Default)]
    public ToolFunctionParameters Parameters { get; set; } = new ToolFunctionParameters();
}

public class ToolCallFunction
{
    [JsonProperty("name", Required = Required.Always)]
    public string Name { get; set; }

    [JsonProperty("arguments", Required = Required.Always)]
    public string Arguments { get; set; }

    public override string ToString()
    {
        return $"{Name}({Arguments})";
    }
}

public class Tool
{
    [JsonProperty("type")]
    public string Type = "function";

    [JsonProperty("function")]
    public ToolFunction Function { get; set; }

    [JsonIgnore]
    public ToolCallDelegate? Execute { get; set; }
}

public class ToolCall
{
    [JsonProperty("id", Required = Required.Always)]
    public string Id { get; set; }

    [JsonProperty("type", Required = Required.Always)]
    public string Type = "function";

    [JsonProperty("function", Required = Required.Always)]
    public ToolCallFunction Function { get; set; }
}

public class ResponseFormat
{
    [JsonProperty("type", Required = Required.Always)]
    public string Type { get; set; } = "json_object";
}

public class OpenAiError
{
    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type = string.Empty;
}

public class MalformedSpeechData
{
    [JsonProperty("text", Required = Required.Always)]
    public string Text { get; set; }
}

public delegate Task<Message> ToolCallDelegate(ToolCall tc, CancellationToken tkn);