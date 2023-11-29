
using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;

public class SpeechManager : IMessageProvider, IChatObserver
{
    public bool IsSynthesizing {get; private set; }
    private SpeechRecognizer speechRecognizer;
    private SpeechSynthesizer speechSynthesizer;
    private Func<SoundDeviceTypes> soundDeviceGetter;

    private bool wasInterrupted = false;
    private ConcurrentQueue<Message> newMessageQueue = new ConcurrentQueue<Message>();

    public SpeechManager(SpeechRecognizer speechRecognizer, SpeechSynthesizer speechSynthesizer, Func<SoundDeviceTypes> soundDeviceGetter)
    {
        this.speechRecognizer = speechRecognizer;
        this.speechSynthesizer = speechSynthesizer;
        this.speechSynthesizer.SynthesisStarted += HandleSynthesisStarted;
        this.speechSynthesizer.Synthesizing += HandleSynthesizing;
        this.speechSynthesizer.SynthesisCompleted += HandleSynthesisCompleted;
        this.soundDeviceGetter = soundDeviceGetter;
    }

    public Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var newMessages = newMessageQueue.ToArray();
        newMessageQueue.Clear();
        return Task.FromResult(newMessages.AsEnumerable());
    }

    public void InterruptSynthesis()
    {
        wasInterrupted = true;
        Console.WriteLine("Begin stop speaking");
        speechSynthesizer.StopSpeakingAsync().ContinueWith((task) => Console.WriteLine("End stop speaking"));
    }

    public void OnNewMessages(IEnumerable<Message> messages)
    {
        messages = messages.Where(m => m.Role == Role.Assistant && string.IsNullOrEmpty(m.Content) == false);
        var speakTask = Task.Run(async () =>
        {
            foreach (var message in messages)
            {
                var speechResult = await SpeakAsync(message);
            }
        });
    }

    public async Task<SpeechSynthesisResult> SpeakAsync(Message message)
    {
        var soundDevice = soundDeviceGetter.Invoke();
        if (soundDevice == SoundDeviceTypes.OpenAirSpeakers)
            await speechRecognizer.StopContinuousRecognitionAsync();
        
        var result = await speechSynthesizer.SpeakTextAsync(message.Content);
        CheckForInterruption(message);
        // DEBUG 
        if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
            if (cancellation.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
            }
        }
        if (soundDevice == SoundDeviceTypes.OpenAirSpeakers)
            await speechRecognizer.StartContinuousRecognitionAsync();
        return result;
    }

    private void CheckForInterruption(Message message)
    {
        var interrupted = wasInterrupted;
        wasInterrupted = false;
        if (interrupted)
        {
            if (message.Content.Length > 10)
            {
                message.Content = message.Content.Substring(0, 10) + "—";
            }
            var scolding = new Message
            {
                Role = Role.System,
                Content = $"The Client interrupted."
            };
            newMessageQueue.Enqueue(scolding);
        }
    }

    private void HandleSynthesisStarted(object? sender, SpeechSynthesisEventArgs e)
    {
        IsSynthesizing = true;
    }

    private void HandleSynthesizing(object? sender, SpeechSynthesisEventArgs e)
    {
        IsSynthesizing = true;
    }

    private void HandleSynthesisCompleted(object? sender, SpeechSynthesisEventArgs e)
    {
        IsSynthesizing = false;
        if (wasInterrupted)
        {
            // Handle interruption case
            Console.WriteLine("[Debug] Interrupted");
        }
    }
}
