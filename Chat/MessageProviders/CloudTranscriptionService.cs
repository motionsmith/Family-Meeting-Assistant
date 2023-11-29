using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;

public class CloudTranscriptionService : IMessageProvider, ITranscriptionService
{
    private SpeechRecognizer speechRecognizer;

    private ConcurrentQueue<Message> messageQueue = new ConcurrentQueue<Message>();

    public bool IsTranscribing { get; private set; }

    private Action<bool> transcribeSettingSetter;
    private Func<bool> transcribeSettingGetter;
    public CloudTranscriptionService(SpeechRecognizer speechRecognizer, SettingsManager settingsManager)
    {
        this.speechRecognizer = speechRecognizer;
        this.transcribeSettingSetter = settingsManager.SetterFor<TranscribeSetting, bool>();
        this.transcribeSettingGetter = settingsManager.GetterFor<TranscribeSetting, bool>();

        speechRecognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                var recognitionMessage = new Message
                {
                    Content = e.Result.Text,
                    Role = Role.User
                };
                messageQueue.Enqueue(recognitionMessage);
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
        };

        speechRecognizer.SessionStarted += (s, e) =>
        {
            IsTranscribing = true;
        };

        speechRecognizer.SessionStopped += (s, e) =>
        {
            IsTranscribing = false;
        };
    }

    public async Task StartTranscriptionAsync(bool updateUserPreference)
    {
        await speechRecognizer.StartContinuousRecognitionAsync();

        Console.WriteLine($"[Debug] Transcription service changed setting to {transcribeSettingGetter.Invoke()}");
        if (updateUserPreference)
        {
            transcribeSettingSetter.Invoke(true);
            messageQueue.Enqueue(new Message
            {
                Role = Role.Assistant,
                Content = "Hello. I can hear you now."
            });
        }
    }

    public async Task StopTranscriptionAsync(bool updateUserPreference)
    {
        await speechRecognizer.StopContinuousRecognitionAsync();

        if (updateUserPreference)
        {
            transcribeSettingSetter.Invoke(false);
            Console.WriteLine($"[Debug] Transcription service changed setting to {transcribeSettingGetter.Invoke()}");
            messageQueue.Enqueue(new Message
            {
                Role = Role.Assistant,
                Content = "I've stopped listening. You can say my name to wake me up."
            });
        }
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var newMessages = messageQueue.ToArray(); // Garbage
        messageQueue.Clear();
        return newMessages;
    }
}
