public class MessageProviderManager
{

    private readonly List<IMessageProvider> messageProviders = new ();

    public MessageProviderManager(IEnumerable<IMessageProvider> messageProviders)
    {
        this.messageProviders = messageProviders.ToList();
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cancelTokenSource)
    {
        var getNewMessagesTasks = messageProviders.Select(mp => mp.GetNewMessagesAsync(cancelTokenSource));
        var results = await Task.WhenAll(getNewMessagesTasks);
        return results.SelectMany(messages => messages);
    }
}