
public class ConsoleChatObserver : IChatObserver
{
    public void OnNewMessages(IEnumerable<Message> messages)
    {
        // DEBUG
        foreach (var m in messages)
        {
            Console.WriteLine($"[{m.Role}] \"{m.Content}\"");
        }
    }
}