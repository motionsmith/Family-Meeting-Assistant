
using Microsoft.CognitiveServices.Speech;
using Newtonsoft.Json.Linq;

public class SpeechManager
{
    private SpeechRecognizer speechRecognizer;
    private SpeechSynthesizer speechSynthesizer;
    private string assistantName;

    public SpeechManager(SpeechRecognizer speechRecognizer, SpeechSynthesizer speechSynthesizer, string assistantName)
    {
        this.speechRecognizer = speechRecognizer;
        this.speechSynthesizer = speechSynthesizer;
        this.assistantName = assistantName;
    }

    public async Task Speak(string message, CancellationToken cancelToken)
    {
        Console.Write($"{assistantName} Says ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\"{message}\"");
        Console.ResetColor();
        await speechRecognizer.StopContinuousRecognitionAsync();
        var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(message);
        OutputSpeechSynthesisResult(speechSynthesisResult, message);
        await speechRecognizer.StartContinuousRecognitionAsync();
    }

    public async Task<Message> SpeakFromToolCall(ToolCall toolCall, CancellationToken cancelToken)
    {
        var arguments = toolCall.Function.Arguments;
        var argsJObj = JObject.Parse(arguments);

        var textToSpeak = argsJObj["text"]?.ToString();
        var speakContent = $"Spoke {textToSpeak}";
        await Speak(textToSpeak, cancelToken);
        return new Message
        {
            Content = speakContent,
            Role = Role.Tool,
            ToolCallId = toolCall.Id
        };
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
