public class MessageManager
{
    public List<Message> ChatCompletionRequestMessages
    {
        get
        {
            return Messages;
        }
    }

    public List<Message> Messages {get;} = new List<Message>();

    private string assistantName;

    public MessageManager(string assistantName)
    {
        this.assistantName = assistantName;
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
            string initialPromptFileText = await File.ReadAllTextAsync(initialPromptFilePath);
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
