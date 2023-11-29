using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;

public class CloudTranscriptionService : IMessageProvider, ITranscriptionService
{
    private SpeechRecognizer speechRecognizer;

    private ConcurrentQueue<Message> messageQueue = new ConcurrentQueue<Message>();

    public bool IsTranscribing { get; private set; }

    public CloudTranscriptionService(SpeechRecognizer speechRecognizer)
    {
        this.speechRecognizer = speechRecognizer;
        
        speechRecognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                var recognitionMessage = new Message {
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

    public async Task StartTranscriptionAsync(bool addSystemMessage)
    {
        await speechRecognizer.StartContinuousRecognitionAsync();
        if (addSystemMessage)
        {
            messageQueue.Enqueue(new Message{
                Role = Role.System,
                Content = "You activate transcription.",
                FollowUp = true
            });
        }
    }

    public async Task StopTranscriptionAsync(bool addSystemMessage)
    {
        await speechRecognizer.StopContinuousRecognitionAsync();
        if (addSystemMessage)
        {
            messageQueue.Enqueue(new Message{
                Role = Role.System,
                Content = "You deactivate transcription.",
                FollowUp = true
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
