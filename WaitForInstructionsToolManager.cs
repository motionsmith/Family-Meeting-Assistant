using Newtonsoft.Json.Linq;

public class WaitForInstructionsToolManager
{
    public IList<Message> Messages {get;} = new List<Message>();

    public event Action<Message>? MessageArrived;

    public WaitForInstructionsToolManager()
    {

    }

    public async Task<Message> WaitForInstructions(ToolCall toolCall, CancellationToken cancelToken)
    {
        return new Message
        {
            Content = "Assistant is waiting on instructions.",
            Role = Role.Tool,
            ToolCallId = toolCall.Id
        };
    }
}
