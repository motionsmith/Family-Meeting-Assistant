using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

public class OpenAIApi : IMessageProvider, IChatObserver
{
    private readonly HttpClient httpClient = new HttpClient();

    private readonly string chatCompletionPrompt;
    private readonly string toolCallPrompt;
    private IConfiguration? config;
    private ConcurrentQueue<Message> messageQueue = new();
    private Task<OpenAIApiResponse>? completeChatTask;
    private CancellationTokenSource completeChatCts;
    private Func<List<Tool>> toolsDelegate;
    private Func<List<Message>> messagesDelegate;
    private Func<GptModel> gptModelSettingDelegate;

    public OpenAIApi(Func<List<Tool>> toolsDel, Func<List<Message>> messagesDel, Func<GptModel> gptModelSettingDel)
    {
        toolsDelegate = toolsDel;
        messagesDelegate = messagesDel;
        gptModelSettingDelegate = gptModelSettingDel;
        config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        httpClient.Timeout = TimeSpan.FromSeconds(15);

        toolCallPrompt = "\n\n## [[ASSISTANT_NAME]]s instructions for speaking\n\n";
        toolCallPrompt += "\nYour message will cause the text content to be read aloud via text-to-speech over the laptop speakers so that The Client can hear you.";
        toolCallPrompt += "\nYour speaking style sounds like it was meant to be heard, not read.";
        toolCallPrompt += "\nWhen you speak, it will feel delayed to us due to network latency.";
        toolCallPrompt += "\nWhen you speak, your text is spoken slowly and somewhat robotically, so keep your spoken text brief.";
        toolCallPrompt += "\nSince you can only read the transcription, you can only use intuition to figure out who is speaking. Feel free to ask for clarification, but only when necessary, as this is an interruption.";
        toolCallPrompt += "\nWhen speaking, be straightforward, not overly nice.";
        toolCallPrompt += "\nDo not bother to tell us that you are available to help because we already know you're here.";

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

    private async Task<OpenAIApiResponse> CompleteChatAsync(List<Message> messages, CancellationToken cancelToken, ResponseFormat? responseFormat = null, List<Tool>? tools = null)
    {
        var allowTools = tools != null && tools.Count > 0;

        if (allowTools)
        {
            messages[0].Content += toolCallPrompt;
        }
        else
        {
            messages[0].Content += toolCallPrompt; // Identical either way for now
        }

        var next = new ChatCompletionRequest
        {
            Model = GptModelToStringId(gptModelSettingDelegate.Invoke()),
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

        var request = new HttpRequestMessage(HttpMethod.Post, JustStrings.CHAT_COMPLETION_ENDPOINT)
        {
            Content = requestContent,
            Headers = {
                { "Authorization", $"Bearer {config["OPENAI_KEY"]}" },
                { "OpenAI-Organization", config["OPENAI_ORG"] }
            }
        };
        //Console.WriteLine(requestJson);

        try
        {
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
            return responseContentObject;
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"[Debug] {ex.Message}");
            throw;
        }
    }

    private string GptModelToStringId(GptModel gptModel)
    {
        return gptModel == GptModel.Gpt35 ? "gpt-3.5-turbo-1106" : "gpt-4-1106-preview";
    }

    public Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var newMessages = messageQueue.ToArray(); // Garbage
        messageQueue.Clear();
        return Task.FromResult(newMessages.AsEnumerable());
    }

    public void OnNewMessages(IEnumerable<Message> messages)
    {
        messages = messages.Where(m => m.Role != Role.Assistant);
        if (messages.Count() == 0)
            return;

        if (completeChatCts != null && completeChatCts.IsCancellationRequested == false)
        {
            completeChatCts.Cancel();
        }
        completeChatCts = new CancellationTokenSource();
        try
        {
            completeChatTask = CompleteChatAsync(messagesDelegate.Invoke(), completeChatCts.Token, null, toolsDelegate.Invoke());
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("[Debug] API request cancelled");
        }
        completeChatTask.ContinueWith(HandleChatCompletion);
    }

    private async Task HandleChatCompletion(Task<OpenAIApiResponse> completedTask)
    {
        if (completedTask.Exception != null)
        {
            // Do something when request throws exception?
            return;
        }
        Message message;
        if (completedTask.Result.Error != null)
        {
            message = new Message
            {
                Role = Role.System,
                Content = $"Error from OpenAI API: {completedTask.Result.Error.Message}"
            };
        }
        else
        {
            message = completedTask.Result.Choices[0].Message;
        }

        messageQueue.Enqueue(message);

        // Handle tool calling
        bool calledTools = message.ToolCalls != null && message.ToolCalls.Count > 0;
        if (calledTools == false)
        {
            return;
        }

        var toolCallsToken = new CancellationTokenSource().Token;
        foreach (var call in message.ToolCalls)
        {
            var functionName = call.Function.Name;
            var arguments = call.Function.Arguments;
            Console.WriteLine($"[{JustStrings.ASSISTANT_NAME}] {functionName}({arguments})");
            Message toolMessage;
            var tool = toolsDelegate.Invoke().FirstOrDefault(tool => tool.Function.Name == functionName);
            if (tool != null)
            {
                try
                {
                    toolMessage = await tool.Execute(call, toolCallsToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw ex;
                }
                
            }
            else
            {
                // Handle unknown function
                toolMessage = new Message
                {
                    Content = $"Unknown tool function name {functionName}. Tool call failed.",
                    ToolCallId = call.Id,
                    Role = Role.Tool
                };
            }
            messageQueue.Enqueue(toolMessage);
        }
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