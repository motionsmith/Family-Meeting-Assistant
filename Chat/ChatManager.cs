using System.Diagnostics;
using Newtonsoft.Json;

public class ChatManager
{
    private static TimeSpan loopMinDuration = TimeSpan.FromMilliseconds(100);
    private static readonly string fileName = "message-history.json";
    
    public static async Task<ChatManager> CreateAsync(
        IEnumerable<IChatObserver> observers,
        IEnumerable<IMessageProvider> messageProviders,
        CancellationToken cancelToken)
    {
        var defaultValue = JsonConvert.SerializeObject(new MessageHistory());
        var fileContents = await StringIO.LoadStateAsync(defaultValue, fileName, cancelToken);
        var loadedMessages = JsonConvert.DeserializeObject<MessageHistory>(fileContents);
        var instance = new ChatManager(loadedMessages.Messages, observers, messageProviders);
        return instance;
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
    private CancellationTokenSource? saveCts;

    private ChatManager(
        IEnumerable<Message> initialMessages,
        IEnumerable<IChatObserver> observers,
        IEnumerable<IMessageProvider> messageProviders)
    {
        Messages = new List<Message>(initialMessages);
        this.observers = new List<IChatObserver>(observers);
        this.messageProviders = new List<IMessageProvider>(messageProviders);
    }

    private async Task SaveAsync()
    {
        if (saveCts != null)
        {
            saveCts.Cancel();
        }
        saveCts = new CancellationTokenSource();

        var messagesToSave = Messages.Skip(1).TakeLast(350).SkipWhile(msg => msg.ToolCalls != null || msg.Role == Role.Tool).ToList();

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
    /// <summary>
    /// Adds a user-originated message to the conversation and notifies observers.
    /// </summary>
    /// <param name="content">The text content of the user message.</param>
    public void AddUserMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var message = new Message
        {
            Role = Role.User,
            Content = content,
            FollowUp = true
        };
        AddMessages(new[] { message });
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error in update loop: {ex.Message}");
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