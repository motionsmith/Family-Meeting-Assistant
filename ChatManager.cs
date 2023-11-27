using System.Diagnostics;
using Newtonsoft.Json;

public class ChatManager
{
    private static TimeSpan loopMinDuration = TimeSpan.FromMilliseconds(100);
    private static readonly string fileName = "message-history.json";
    
    public static async Task<ChatManager> CreateAsync(IEnumerable<IChatObserver> observers, IEnumerable<IMessageProvider> messageProviders, Func<List<Tool>> toolsDel, CancellationToken cancelToken)
    {
        var defaultValue = JsonConvert.SerializeObject(new MessageHistory());
        var fileContents = await StringIO.LoadStateAsync(defaultValue, fileName, cancelToken);
        var loadedMessages = JsonConvert.DeserializeObject<MessageHistory>(fileContents);
        var instance = new ChatManager(loadedMessages.Messages, observers, messageProviders, toolsDel);
        return instance;
    }

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

    public List<Message> Messages { get; private set; }
    private readonly List<IChatObserver> observers = new List<IChatObserver>();
    private readonly List<IMessageProvider> messageProviders = new ();
    private readonly OpenAIApi openAi;
    private CancellationTokenSource saveCts;

    private ChatManager(IEnumerable<Message> initialMessages, IEnumerable<IChatObserver> observers, IEnumerable<IMessageProvider> messageProviders, Func<List<Tool>> toolsDel)
    {
        openAi = new OpenAIApi(toolsDel, () => Messages);
        Messages = new List<Message>(initialMessages);
        this.observers = new List<IChatObserver>(observers)
        {
            openAi
        };
        this.messageProviders = new List<IMessageProvider>(messageProviders)
        {
            openAi
        };

    }

    private async Task SaveAsync()
    {
        if (saveCts != null)
        {
            saveCts.Cancel();
        }
        saveCts = new CancellationTokenSource();

        var messagesToSave = Messages.Skip(1).TakeLast(400).SkipWhile(msg => msg.ToolCalls != null || msg.Role == Role.Tool).ToList();

        var messageHistory = new MessageHistory
        {
            Messages = messagesToSave
        };
        var fileContents = await Task.Run(() => JsonConvert.SerializeObject(messageHistory), saveCts.Token);
        await StringIO.SaveStateAsync(fileContents, fileName, saveCts.Token);
    }

    private void AddMessages(IEnumerable<Message> messages)
    {
        if (messages == null || messages.Count() == 0) return;
        Messages.AddRange(messages);
        observers.ForEach(obs => obs.OnNewMessages(messages));
        
        var _ = SaveAsync();
    }

    private string GetFullPromptPath(string fileName)
    {
        var appDataDirPath = Environment.SpecialFolder.ApplicationData.ToString();
        string documentsPath = Path.GetFullPath(appDataDirPath);
        return Path.Combine(documentsPath, fileName);
    }

    public async Task StartContinuousUpdatesAsync()
    {
        while (true)
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                var allNewMessages = await GetNewMessagesAsync(new CancellationTokenSource());
                AddMessages(allNewMessages);
                stopwatch.Stop();
                if (stopwatch.Elapsed < loopMinDuration)
                {
                    await Task.Delay(loopMinDuration - stopwatch.Elapsed);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"Loop cancelled. Enforcing 1s loop delay");
                await Task.Delay(1000);
            }
        }
    }

    private async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cancelTokenSource)
    {
        var getNewMessagesTasks = messageProviders.Select(mp => mp.GetNewMessagesAsync(cancelTokenSource));
        var results = await Task.WhenAll(getNewMessagesTasks);
        return results.SelectMany(messages => messages);
    }
}

public class MessageHistory
{
    [JsonProperty("messages", Required = Required.Always)]
    public List<Message> Messages { get; set; } = new List<Message>();
}