
using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;

public class SpeechManager : IMessageProvider
{
    private SpeechRecognizer speechRecognizer;
    private SpeechSynthesizer speechSynthesizer;
    private ClientSoundDeviceSetting clientSoundDeviceSetting;

    private bool wasInterrupted = false;
    private ConcurrentQueue<Message> newMessageQueue = new ConcurrentQueue<Message>();

    public SpeechManager(SpeechRecognizer speechRecognizer, SpeechSynthesizer speechSynthesizer, ClientSoundDeviceSetting clientSoundDeviceSetting)
    {
        this.speechRecognizer = speechRecognizer;
        this.speechSynthesizer = speechSynthesizer;
        this.speechSynthesizer.SynthesisCompleted += HandleSynthesisCompleted;
        this.clientSoundDeviceSetting = clientSoundDeviceSetting;
    }

    public Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var newMessages = newMessageQueue.ToArray();
        newMessageQueue.Clear();
        return Task.FromResult(newMessages.AsEnumerable());
    }

    public async Task InterruptSpeaking(CancellationToken cancelToken)
    {
        wasInterrupted = true;
        speechSynthesizer.StopSpeakingAsync();
    }

    public async Task<SpeechSynthesisResult> SpeakAsync(Message message)
    {
        Console.WriteLine($"\"{message.Content}\"");
        if (clientSoundDeviceSetting.Value == ClientSoundDeviceSetting.SoundDeviceTypes.OpenAirSpeakers)
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
        if (clientSoundDeviceSetting.Value == ClientSoundDeviceSetting.SoundDeviceTypes.OpenAirSpeakers)
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

    private void HandleSynthesisCompleted(object? sender, SpeechSynthesisEventArgs e)
    {
        if (wasInterrupted)
        {
            // Handle interruption case
            Console.WriteLine("[SpeechManager] Interrupted");
        }
    }
}
