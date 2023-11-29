using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

public class OpenAiChatCompleter : IMessageProvider, IChatObserver, IChatCompleter
{
    private readonly HttpClient httpClient = new HttpClient();
    private IConfiguration? config;
    private ConcurrentQueue<Message> messageQueue = new();
    private Task<OpenAIApiResponse>? completeChatTask;
    private CancellationTokenSource completeChatCts;
    private Func<List<Tool>> toolsDelegate;
    private Func<List<Message>> messagesDelegate;
    private Func<GptModel> gptModelSettingDelegate;
    private Func<InteractionMode> interactionModeDelegate;

    public bool IsCompletionTaskRunning => completeChatTask != null && completeChatTask.IsCompleted == false;

    public OpenAiChatCompleter(
        Func<List<Tool>> toolsDel,
        Func<List<Message>> messagesDel,
        SettingsManager settingsManager)
    {
        toolsDelegate = toolsDel;
        messagesDelegate = messagesDel;
        gptModelSettingDelegate = settingsManager.GetterFor<GptModelSetting, GptModel>();
        interactionModeDelegate = settingsManager.GetterFor<InteractionModeSetting, InteractionMode>();
        config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public void RequestChatCompletion()
    {
        if (completeChatCts != null && completeChatCts.IsCancellationRequested == false)
        {
            completeChatCts.Cancel();
        }
        completeChatCts = new CancellationTokenSource();
        completeChatTask = CompleteChatAsync(messagesDelegate.Invoke(), completeChatCts.Token, null, toolsDelegate.Invoke());
        completeChatTask.ContinueWith(HandleChatCompletion);
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

        var wakeWordMode = interactionModeDelegate.Invoke() == InteractionMode.Passive;
        var followUp = messages.Any((Message m) => m.FollowUp);
        if (!wakeWordMode || WakeWordSpokenInMessages(messages) || followUp)
        {
            RequestChatCompletion();
        }

    }

    private async Task<OpenAIApiResponse> CompleteChatAsync(List<Message> messages, CancellationToken cancelToken, ResponseFormat? responseFormat = null, List<Tool>? tools = null)
    {
        var allowTools = tools != null && tools.Count > 0;

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

        int retries = 0;
        do
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, JustStrings.CHAT_COMPLETION_ENDPOINT)
                {
                    Content = requestContent,
                    Headers = {
                        { "Authorization", $"Bearer {config["OPENAI_KEY"]}" },
                        { "OpenAI-Organization", config["OPENAI_ORG"] }
                    }
                };
                //Console.WriteLine(requestJson);

                var httpResponse = await httpClient.SendAsync(request, cancelToken);
                if (httpResponse.IsSuccessStatusCode == false)
                {
                    var failureResponseContent = await httpResponse.Content.ReadAsStringAsync();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"OPENAI ERROR - {httpResponse.StatusCode} {httpResponse.ReasonPhrase} {failureResponseContent}");
                    Console.WriteLine($"REQUEST DUMP:\n\n{requestJson}");
                    Console.ResetColor();

                }
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                var responseContentObject = JsonConvert.DeserializeObject<OpenAIApiResponse>(responseContent);
                return responseContentObject;
            }
            catch (TaskCanceledException ex)
            {
                retries++;
                Console.WriteLine(ex.Message);
            }
        } while (retries <= 2);
        throw new TimeoutException("Tried API request 3 times--cancelling to avoid indefinite loop.");
    }

    private bool WakeWordSpokenInMessages(IEnumerable<Message> messages)
    {
        string pattern = @"(?i:(?<![\'""])(potatoes)(?![\'""]))";
        pattern = pattern.Replace("potatoes", JustStrings.ASSISTANT_NAME.ToLower());
        messages = messages.Where(m => m.Role == Role.User);
        foreach (var msg in messages)
        {
            bool messageContainsLiteral = string.IsNullOrEmpty(msg.Content) == false && Regex.IsMatch(msg.Content, pattern);
            if (messageContainsLiteral) return true;
        }
        return false;
    }

    private async Task HandleChatCompletion(Task<OpenAIApiResponse> completedTask)
    {
        // Sometimes we need to change the completed message.
        Message message;
        if (completedTask.Exception != null)
        {
            if (completedTask.Exception.InnerException is TimeoutException)
            {
                message = new Message
                {
                    Role = Role.Assistant,
                    Content = $"My server isn't responding. Check out your internet connection."
                };
            }
            else
            {
                Console.WriteLine($"[Debug] Unhandled exception: {completedTask.Exception.Message}");
                return;
            }
            // Do something when request throws exception?
        }
        else
        {
            if (completedTask.Result.Error != null)
            {
                message = new Message
                {
                    Role = Role.Assistant,
                    Content = $"Error from OpenAI API! {completedTask.Result.Error.Message}"
                };
            }
            else
            {
                message = completedTask.Result.Choices[0].Message;
            }
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

    private string GptModelToStringId(GptModel gptModel)
    {
        return gptModel == GptModel.Gpt35 ? "gpt-3.5-turbo-1106" : "gpt-4-1106-preview";
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