using System.Diagnostics;

public class TimeMessageProvider : IMessageProvider
{
    public Tool GetTimeTool = new Tool {
        Function = new ToolFunction
        {
            Name = "get_current_time",
            Description = "Retrieves the current time."
        }
    };

    private DateTime lastReport = DateTime.UnixEpoch;

    public TimeMessageProvider()
    {
        GetTimeTool.Execute = (x, y) => Task.FromResult(
            new Message {
                Role = Role.Tool,
                ToolCallId = x.Id, Content = DateTime.Now.ToString(),
                FollowUp = true
                });
    }

    public Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var messages = new List<Message>();
        var now = DateTime.Now;
        var duration = now - lastReport;
        var thres = TimeSpan.FromMinutes(1);
        if (duration > thres)
        {
            var pre = "Current time";
            if (duration.TotalDays > 10)
                pre = "Session started at"; // Just inidicating this is the first comparison, now - epoch
            messages.Add(new Message
            {
                Role = Role.System,
                Content = $"{pre}: {DateTime.Now}."
            });
            lastReport = now;
        }
        
        return Task.FromResult(messages.AsEnumerable());
    }
}