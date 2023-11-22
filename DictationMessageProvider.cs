using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

public class DictationMessageProvider
{
    private SpeechRecognizer speechRecognizer;

    private ConcurrentQueue<Message> messageQueue = new ConcurrentQueue<Message>();

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
                     Content = $"### Speech transcription:\n\n_The Client_ ({DateTime.Now.ToShortTimeString()}):\n{e.Result.Text}\n",
                     Role = Role.User
                };
                messageQueue.Enqueue(recognitionMessage);

                // Debug
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"Heard \"{e.Result.Text}\"");
                Console.ResetColor();
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                //Console.WriteLine($"NOMATCH: Speech could not be recognized.");
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

    public async Task StopContinuousRecognitionAsync()
    {
        await speechRecognizer.StopContinuousRecognitionAsync();
    }

    public async Task<Message> ReadLine(string userName, CancellationToken cancelToken)
    {
        var userInputText = Console.ReadLine();
        return new Message { Content = $"{DateTime.Now.ToShortTimeString()}\n{userName} (typed): {userInputText}", Role = Role.User };
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationToken cancelToken)
    {
        var newMessages = messageQueue.ToArray(); // Garbage
        messageQueue.Clear();
        return newMessages;
    }

    internal async Task StartContinuousRecognitionAsync()
    {
        await speechRecognizer.StartContinuousRecognitionAsync();
    }
}
