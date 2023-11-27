using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;

public static class KeyboardSpeechInterrupter
{
    private static TimeSpan loopMinDuration = TimeSpan.FromMilliseconds(100);

    public static Task StartKeyboardInterrupter(SpeechManager speechManager)
    {
        return Task.Run(async () =>
        {
            while (true)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                var key = Console.ReadKey(intercept: true); // 'intercept: true' prevents the key from being displayed
                if (key.Key == ConsoleKey.Spacebar)
                {
                    speechManager.InterruptSpeaking();
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