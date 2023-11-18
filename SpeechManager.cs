
using Microsoft.CognitiveServices.Speech;
using Newtonsoft.Json.Linq;

public class SpeechManager : IMessageProvider
{
    private SpeechConfig speechConfig;
    private SpeechRecognizer speechRecognizer;
    private SpeechSynthesizer speechSynthesizer;

    public IList<Message> Messages { get; } = new List<Message>();

    public event Action<Message>? MessageArrived;

    public SpeechManager(SpeechConfig speechConfig, SpeechRecognizer speechRecognizer, SpeechSynthesizer speechSynthesizer)
    {
        this.speechConfig = speechConfig;
        this.speechRecognizer = speechRecognizer;
        this.speechSynthesizer = speechSynthesizer;
    }

    public async Task Speak(ToolCall toolCall)
    {
        var functionName = toolCall.Function.Name;
        var arguments = toolCall.Function.Arguments;
        var argsJObj = JObject.Parse(arguments);

        var textToSpeak = argsJObj["text"]?.ToString();
        var speakContent = $"Spoke {textToSpeak}";
        await speechRecognizer.StopContinuousRecognitionAsync();
        var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(textToSpeak);
        OutputSpeechSynthesisResult(speechSynthesisResult, textToSpeak);
        await speechRecognizer.StartContinuousRecognitionAsync();
        var message = new Message
        {
            Content = speakContent,
            Role = Role.Tool,
            ToolCallId = toolCall.Id
        };
        Messages.Add(message);
        MessageArrived?.Invoke(message);
    }

    void OutputSpeechSynthesisResult(SpeechSynthesisResult speechSynthesisResult, string text)
    {
        switch (speechSynthesisResult.Reason)
        {
            case ResultReason.SynthesizingAudioCompleted:
                break;
            case ResultReason.Canceled:
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                if (cancellation.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                    Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                }
                break;
            default:
                break;
        }
    }
}
