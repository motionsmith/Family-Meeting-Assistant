public interface IMessageProvider
{
    public IList<Message> Messages { get; }
    public event Action<Message>? MessageArrived;
}
