
public interface ISetting : IMessageProvider
{
    dynamic Value {get; set;}
    // Define any common non-generic functionality here
    string SerializedValue { get; protected set; }
    Tool GetSettingTool {get;}
    Tool SetSettingTool {get;}

    Task<Message> GetGetSettingMessageAsync(ToolCall tc, CancellationToken tkn);
    Task<Message> UpdateValueAsync(ToolCall tc, CancellationToken tkn);
}