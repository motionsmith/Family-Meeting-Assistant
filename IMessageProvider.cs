public interface IMessageProvider
{
    Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts);
}