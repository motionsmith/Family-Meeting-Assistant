using Newtonsoft.Json;

public class ChatManager
{
    public List<Message> ChatCompletionRequestMessages
    {
        get
        {
            return Messages;
        }
    }

    public Message PinnedMessage
    {
        get
        {
            return Messages[0];
        }
        set
        {
            Messages[0] = value;
        }
    }

    public List<Message> Messages {get; private set;} = new List<Message>();

    public ChatManager()
    {
        
    }

    public async Task LoadAsync(CancellationToken cancelToken)
    {
        var messageHistoryFilePath = GetFullPromptPath("message_history.json");
        Messages = new List<Message> {
            new Message { 
                Role = Role.System,
                Content = "You are a helpful assistant."
            }
        };
        if (File.Exists(messageHistoryFilePath))
        {
            string messageHistoryContent = await File.ReadAllTextAsync(messageHistoryFilePath, cancelToken);
            var loadedMessages = JsonConvert.DeserializeObject<MessageHistory>(messageHistoryContent);
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

    public void AddMessages(IEnumerable<Message> messages)
    {
        Messages.AddRange(messages);
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