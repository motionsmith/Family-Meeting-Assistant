using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

public class DictationMessageProvider : IMessageProvider
{
    public event Action<Message>? MessageArrived;

    private SpeechRecognizer speechRecognizer;

    public IList<Message> Messages {get;} = new List<Message>();

    public DictationMessageProvider(SpeechRecognizer speechRecognizer)
    {
        this.speechRecognizer = speechRecognizer;

        var stopRecognition = new TaskCompletionSource<int>();
        speechRecognizer.Recognizing += (s, e) =>
        {
            //Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
        };

        speechRecognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                var recognitionMessage = new Message {
                     Content = $"{DateTime.Now.ToShortTimeString()}: {e.Result.Text}",
                     Role = Role.User
                };
                Messages.Add(recognitionMessage);
                MessageArrived?.Invoke(recognitionMessage);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Heard \"{e.Result.Text}\"");
                Console.ResetColor();
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
            }
        };

        speechRecognizer.Canceled += (s, e) =>
        {
            Console.WriteLine($"CANCELED: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
            }

            stopRecognition.TrySetResult(0);
        };

        speechRecognizer.SessionStopped += (s, e) =>
        {
            stopRecognition.TrySetResult(0);
        };
    }

    internal async Task StopContinuousRecognitionAsync()
    {
        await speechRecognizer.StopContinuousRecognitionAsync();
    }

    internal async Task StartContinuousRecognitionAsync()
    {
        await speechRecognizer.StartContinuousRecognitionAsync();
    }
}
