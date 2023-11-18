using Newtonsoft.Json.Linq;

public class WaitForInstructionsToolManager : IMessageProvider
{
    public IList<Message> Messages {get;} = new List<Message>();

    public event Action<Message>? MessageArrived;

    public WaitForInstructionsToolManager()
    {

    }

    public void WaitForInstructions(ToolCall toolCall)
    {
        var functionName = toolCall.Function.Name;
        var arguments = toolCall.Function.Arguments;
        var argsJObj = JObject.Parse(arguments);

        var doNothingContent = $"Mhm";
        var message = new Message
        {
            Content = doNothingContent,
            Role = Role.Tool,
            ToolCallId = toolCall.Id
        };
        Messages.Add(message);
        MessageArrived?.Invoke(message);
    }
}
