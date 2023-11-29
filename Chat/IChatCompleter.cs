public interface IChatCompleter
{
    public bool IsCompletionTaskRunning { get; }
    void RequestChatCompletion();
}