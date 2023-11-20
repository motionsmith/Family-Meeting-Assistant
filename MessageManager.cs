using Newtonsoft.Json;

public class MessageManager
{
    public List<Message> ChatCompletionRequestMessages
    {
        get
        {
            return Messages;
        }
    }

    public List<Message> Messages {get; private set;} = new List<Message>();

    private string assistantName;

    public MessageManager(string assistantName)
    {
        this.assistantName = assistantName;
    }

    public async Task LoadAsync(CancellationToken cancelToken)
    {
        var messageHistoryFilePath = GetFullPromptPath("message_history.json");
        if (File.Exists(messageHistoryFilePath))
        {
            string messageHistoryContent = await File.ReadAllTextAsync(messageHistoryFilePath, cancelToken);
            var loadedMessages = JsonConvert.DeserializeObject<MessageHistory>(messageHistoryContent);

            var initialSystemMessage = await CreateInitialSystemPrompt(cancelToken);
            Messages = new List<Message> {
                initialSystemMessage
            };
            Messages.AddRange(loadedMessages.Messages);
        }
        else
        {
            Console.WriteLine($"Message History file {messageHistoryFilePath} does not exist.");
            await SaveAsync(cancelToken);
        }
    }

    public async Task SaveAsync(CancellationToken cancelToken)
    {
        var messagesToSave = Messages.Skip(1).TakeLast(128).SkipWhile(msg => msg.ToolCalls != null || msg.Role == Role.Tool).ToList();
        
        var messageHistory = new MessageHistory {
            Messages = messagesToSave
        };
        var fileContents = JsonConvert.SerializeObject(messageHistory);
        var filePath = GetFullPromptPath("message_history.json");
        await File.WriteAllTextAsync(filePath, fileContents, cancelToken);
    }

    public void AddMessage(Message message)
    {
        Messages.Add(message);
    }

    public void AddMessages(IEnumerable<Message> toolMessages)
    {
        Messages.AddRange(toolMessages);
    }

    public string InsertPromptVariables(string prompt)
    {
        return prompt
                .Replace("\"", "\\\"")
                .Replace(Environment.NewLine, "\\n")
                .Replace("[[ASSISTANT_NAME]]", assistantName)
                .Replace("[[NOW]]", DateTime.Now.ToString());
    }

    public async Task<Message> CreateInitialSystemPrompt(CancellationToken cancelToken)
    {
        var initialPromptFilePath = GetFullPromptPath("prompt.txt");
        var initialPromptString = "Tell me a joke";
        if (File.Exists(initialPromptFilePath))
        {
            string initialPromptFileText = await File.ReadAllTextAsync(initialPromptFilePath, cancelToken);
            initialPromptString = InsertPromptVariables(initialPromptFileText);
        }
        else
        {
            Console.WriteLine($"Prompt file {initialPromptFilePath} does not exist.");
        }
        return new Message
        {
            Content = initialPromptString,
            Role = Role.System
        };
    }

    private string GetFullPromptPath(string fileName)
    {
        var appDataDirPath = Environment.SpecialFolder.ApplicationData.ToString();
        string documentsPath = Path.GetFullPath(appDataDirPath);
        return Path.Combine(documentsPath, fileName);
    }
}

public class MessageHistory
{
    [JsonProperty("messages", Required = Required.Always)]
    public List<Message> Messages { get; set; } = new List<Message>();
}