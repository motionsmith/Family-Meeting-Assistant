using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

public class DictationMessageProvider : IMessageProvider
{
    private SpeechRecognizer speechRecognizer;
    private SpeechSynthesizer speechSynthesizer;

    private Task? cancelSynthTask;

    private ConcurrentQueue<Message> messageQueue = new ConcurrentQueue<Message>();
    private bool isSynthesizing;

    public DictationMessageProvider(SpeechRecognizer speechRecognizer, SpeechSynthesizer speechSynthesizer)
    {
        this.speechRecognizer = speechRecognizer;
        this.speechSynthesizer = speechSynthesizer;
        
        speechSynthesizer.SynthesisStarted += OnSynthStarted;
        speechSynthesizer.SynthesisCanceled += OnSynthCancelled;
        speechSynthesizer.SynthesisCompleted += OnSynthCompleted;
        
        speechRecognizer.Recognizing += CancelSynthesis;
        
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

        speechRecognizer.SessionStopped += (s, e) =>
        {
            
        };
    }

    private void OnSynthStarted(object? sender, SpeechSynthesisEventArgs e)
    {
       isSynthesizing = true;
    }

    private void OnSynthCompleted(object? sender, SpeechSynthesisEventArgs e)
    {
        isSynthesizing = false;
        cancelSynthTask = null;
    }

    private void OnSynthCancelled(object? sender, SpeechSynthesisEventArgs e)
    {
        cancelSynthTask = null;
    }

    private void CancelSynthesis(object? sender, SpeechRecognitionEventArgs e)
    {
        if (isSynthesizing && cancelSynthTask == null)
        {
            cancelSynthTask = speechSynthesizer.StopSpeakingAsync();
        }
    }

    public async Task StopContinuousRecognitionAsync()
    {
        await speechRecognizer.StopContinuousRecognitionAsync();
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
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
