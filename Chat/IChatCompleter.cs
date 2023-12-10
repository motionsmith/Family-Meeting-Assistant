public interface IChatCompleter
{
    public event Action ChatCompletionRequested;
    public bool IsCompletionTaskRunning { get; }
    void RequestChatCompletion();
}