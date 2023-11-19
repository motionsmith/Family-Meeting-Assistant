public class MessageManager
{
    private List<Message> messages = new List<Message>();
    
    private string initialPrompString = $"You are being called from an instance of an app that has failed to load the file that contains your instructions. The user can specify the prompt file by using the argument \"-prompt <path-to-file.txt>\" or by adding a file called \"prompt.txt\" to the folder \"{Environment.SpecialFolder.ApplicationData}\". You always respond with a short joke in the style of Seinfeld. The joke also clearly informs the user of the problem.";
    public MessageManager(string promptArg, string assistantName)
    {
        var initialPromptFilePath = GetFullPromptPath("prompt.txt");
        if (string.IsNullOrEmpty(promptArg) == false)
        {
            var promptArgFilePath = GetFullPromptPath(promptArg);
            initialPromptFilePath = promptArgFilePath;
        }
        if (File.Exists(initialPromptFilePath))
        {
            string initialPromptFileText = File.ReadAllText(initialPromptFilePath);
            initialPrompString = initialPromptFileText
                .Replace("\"", "\\\"")
                .Replace(Environment.NewLine, "\\n")
                .Replace("[[ASSISTANT_NAME]]", assistantName)
                .Replace("[[NOW]]", DateTime.Now.ToString());
        }
        else
        {
            Console.WriteLine($"Prompt file {initialPromptFilePath} does not exist.");
        }
    }
    
    public List<Message> GetChatCompletionRequestMessages()
    {
        var messages = new List<Message> {
                    GetInitialSystemMessage()
                };
            messages.AddRange(this.messages);
            return messages;
    }

    public void AddMessage(Message message)
    {
        messages.Add(message);
    }

    public void AddMessages(IEnumerable<Message> toolMessages)
    {
        messages.AddRange(toolMessages);
    }

    private Message GetInitialSystemMessage()
    {
        var initialSystemPrompt = initialPrompString;
        //var choresPrompt = $"\nFamily task and chore list:\n{ChoreManager.PromptList}\n";
        return new Message
        {
            Content = initialSystemPrompt/* + choresPrompt*/,
            Role = Role.System
        };
    }

    private string GetFullPromptPath(string fileArg)
    {
        var appDataDirPath = Environment.SpecialFolder.ApplicationData.ToString();
        string documentsPath = Path.GetFullPath(appDataDirPath);
        return Path.Combine(documentsPath, fileArg);
    }
}
