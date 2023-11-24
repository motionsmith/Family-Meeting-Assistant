
using Microsoft.CognitiveServices.Speech;
using Newtonsoft.Json;
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

    public async Task Speak(string message, CancellationToken cancelToken, bool autoPauseSpeechRecognizer = false)
    {
        if (string.IsNullOrEmpty(message)) return;

        Console.Write($"{assistantName} Says ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\"{message}\"");
        Console.ResetColor();
        if (autoPauseSpeechRecognizer)
            await speechRecognizer.StopContinuousRecognitionAsync();
        var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(message);
        OutputSpeechSynthesisResult(speechSynthesisResult, message);
        if (autoPauseSpeechRecognizer)
            await speechRecognizer.StartContinuousRecognitionAsync();
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
