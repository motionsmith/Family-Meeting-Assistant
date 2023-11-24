using System.Diagnostics;

public class TimeMessenger
{
    private static DateTime lastReport = DateTime.UnixEpoch;

    public static Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationToken cancelToken)
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
                Content = $"{DateTime.Now}"
            });
            lastReport = now;
        }
        
        return Task.FromResult(messages.AsEnumerable());
    }
}