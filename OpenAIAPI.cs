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
    private readonly List<Tool> _tools = new List<Tool> {
            new Tool {
                Function = new ToolFunction {
                    Name = "file_task",
                    Description = "Adds a task to the family task list.",
                    Parameters = new ToolFunctionParameters {
                        Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "title", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "A short description that helps the family members remember what needs to be done to complete this task."
                                }
                            }
                        },
                        Required = new List<string> { "title" }
                    }
                }
            },
            new Tool {
                Function = new ToolFunction
                {
                    Name = "list_tasks",
                    Description = "Lists the tasks in the family task list."
                }
            },
            new Tool {
                Function = new ToolFunction {
                    Name = "complete_task",
                    Description = "Removes a task from the family task list.",
                    Parameters = new ToolFunctionParameters {
                        Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "title", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The title of the task to be removed."
                                }
                            }
                        },
                        Required = new List<string> { "title" }
                    }
                }
            },
            new Tool {
                Function = new ToolFunction
                {
                    Name = "speak",
                    Description = "Causes the LLM to speak using text-to-speech though the user's speakers.",
                    Parameters = new ToolFunctionParameters
                    {
                        Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "text", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The text to be spoken."
                                }
                            }
                        },
                        Required = new List<string> { "text" }
                    }
                }
            },
            new Tool {
                Function = new ToolFunction
                {
                    Name = "do_nothing",
                    Description = "Call this function when there are no actions to be taken."
                }
            }
        };

    public async Task<OpenAIApiResponse> SendRequestAsync(string initialPrompString, IEnumerable<MeaningfulChunk> chunks)
    {
        var next = new ChatCompletionRequest
        {
            Model = modelId,
            Messages = CreateMessages(initialPrompString, chunks),
            Temperature = 0.7,
            Tools = _tools,
            ResponseFormat = new ResponseFormat
            {
                Type = "json_object"
            }
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
            {
                var failureResponseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response - {response.StatusCode} {response.ReasonPhrase} {failureResponseContent}");
            }

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var responseContentObject = JsonConvert.DeserializeObject<OpenAIApiResponse>(responseContent);
            //Console.WriteLine(responseContent);
            return responseContentObject;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OpenAI request failed: {ex} {ex.Message} {requestJson}");
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
                var aiResponseMessage = chunk.OpenAITask.Result.Choices[0].Message;
                messages.Add(aiResponseMessage);

                // According to OpenAI service: "An assistant message with 'tool_calls' must be followed by tool messages responding to each 'tool_call_id'."
                if (chunk.ToolCallTasks != null && chunk.ToolCallTasks.Result != null)
                    messages.AddRange(chunk.ToolCallTasks.Result);

            }
        }

        return messages;
    }

    private Tool GetToolByName(string toolName)
    {
        return _tools.First(tool => tool.Function.Name == toolName);
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
    [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
    public string Content { get; set; }  // Can be null according to documentation

    [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
    public List<ToolCall>? ToolCalls { get; set; }

    [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? ToolCallId { get; set; }

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

    public Func<string>? Log { get; set; }
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
    public string Type { get; set; }
}
