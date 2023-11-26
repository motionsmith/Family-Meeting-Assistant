
using Newtonsoft.Json.Linq;

public abstract class SettingBase : ISetting
{
    public abstract Tool GetSettingTool { get; }
    public abstract Tool SetSettingTool { get; }

    public dynamic Value { get; set; }

    public abstract string SerializedValue { get; set; }

    public SettingBase(string defaultValue)
    {
        SerializedValue = defaultValue;
    }

    public abstract Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts);

    public Task<Message> GetGetSettingMessageAsync(ToolCall tc, CancellationToken tkn)
    {
        var msg = new Message
        {
            Content = $"The Client Sound Device Setting value is {Value}",
            Role = Role.Tool,
            ToolCallId = tc.Id
        };
        return Task.FromResult(msg);
    }

    public async Task<Message> UpdateValueAsync(ToolCall tc, CancellationToken tkn)
    {
        var args = JObject.Parse(tc.Function.Arguments);
        SerializedValue = (string)args["value"];
        return new Message
        {
            Content = $"The Setting \"{GetType().Name}\" has been changed to {Value}. Quickly confirm.",
            Role = Role.Tool,
            ToolCallId = tc.Id,
            FollowUp = true
        };
    }
}