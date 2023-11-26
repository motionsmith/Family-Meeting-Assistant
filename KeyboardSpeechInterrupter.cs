using Microsoft.CognitiveServices.Speech;

public static class KeyboardSpeechInterrupter
{
    public static Task StartKeyboardInterrupter(SpeechManager speechManager, CancellationToken cancelToken)
    {
        return Task.Run(async () =>
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true); // 'intercept: true' prevents the key from being displayed
                if (key.Key == ConsoleKey.Spacebar)
                {
                    await speechManager.InterruptSpeaking(cancelToken);
                }
            }
        }, cancelToken);
    }
}