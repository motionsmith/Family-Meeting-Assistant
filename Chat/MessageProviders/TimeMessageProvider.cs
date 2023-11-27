using System.Diagnostics;

public class TimeMessageProvider : IMessageProvider
{
    private DateTime lastReport = DateTime.UnixEpoch;

    public Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var messages = new List<Message>();
        var now = DateTime.Now;
        var duration = now - lastReport;
        var thres = TimeSpan.FromMinutes(1);
        if (duration > thres)
        {
            messages.Add(new Message
            {
                Role = Role.System,
                Content = $"{DateTime.Now}\nThe System time update is for your information. You remain silent \"...\" unless this update is significant."
            });
            lastReport = now;
        }
        
        return Task.FromResult(messages.AsEnumerable());
    }
}