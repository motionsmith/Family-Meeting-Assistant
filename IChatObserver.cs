public interface IChatObserver
{
    void OnNewMessages(IEnumerable<Message> messages);
}