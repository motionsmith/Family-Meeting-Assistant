using System.Diagnostics;
public static class UserCommands
{
    private static TimeSpan loopMinDuration = TimeSpan.FromMilliseconds(100);

    public static event Action? Interrupt;
    public static event Action? RequestChatCompletion;
    public static event Action? ToggleTranscription;

    public static Task StartReadingAsync()
    {
        return Task.Run(async () =>
        {
            while (true)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                var key = Console.ReadKey(intercept: true); // 'intercept: true' prevents the key from being displayed
                if (key.Key == ConsoleKey.Spacebar)
                {
                    ToggleTranscription?.Invoke();
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    RequestChatCompletion?.Invoke();
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    Interrupt?.Invoke();
                }

                stopwatch.Stop();
                if (stopwatch.Elapsed < loopMinDuration)
                {
                    await Task.Delay(loopMinDuration - stopwatch.Elapsed);
                }
            }
        });
    }
}