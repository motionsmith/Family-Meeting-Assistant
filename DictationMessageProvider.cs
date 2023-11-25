using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

public class DictationMessageProvider
{
    private SpeechRecognizer speechRecognizer;
    private SpeechSynthesizer speechSynthesizer;

    private Task? cancelSynthTask;

    private ConcurrentQueue<Message> messageQueue = new ConcurrentQueue<Message>();
    private CancellationTokenSource? interruptCts = null;

    public DictationMessageProvider(SpeechRecognizer speechRecognizer, SpeechSynthesizer speechSynthesizer)
    {
        this.speechRecognizer = speechRecognizer;
        this.speechSynthesizer = speechSynthesizer;

        speechSynthesizer.SynthesisCanceled += OnSynthCancelled;
        
        speechRecognizer.Recognizing += CancelSynthesis;
        
        speechRecognizer.Recognized += (s, e) =>
        {
            cancelSynthTask = null;
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

        speechRecognizer.SessionStopped += (s, e) =>
        {
            
        };
    }

    private void OnSynthCancelled(object? sender, SpeechSynthesisEventArgs e)
    {
        Console.WriteLine("[Dictation] Synth task cancellation complete");
        cancelSynthTask = null;
    }

    private void CancelSynthesis(object? sender, SpeechRecognitionEventArgs e)
    {
        if (cancelSynthTask == null)
        {
            cancelSynthTask = speechSynthesizer.StopSpeakingAsync();
            Console.WriteLine($"Synth task cancellation started.");
            interruptCts?.Cancel();
        }
    }

    public async Task StopContinuousRecognitionAsync()
    {
        await speechRecognizer.StopContinuousRecognitionAsync();
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        interruptCts = cts;
        var newMessages = messageQueue.ToArray(); // Garbage
        messageQueue.Clear();
        return newMessages;
    }

    internal async Task StartContinuousRecognitionAsync()
    {
        await speechRecognizer.StartContinuousRecognitionAsync();
    }
}
