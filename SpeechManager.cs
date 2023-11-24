
using Microsoft.CognitiveServices.Speech;

public class SpeechManager
{
    private SpeechRecognizer speechRecognizer;
    private SpeechSynthesizer speechSynthesizer;

    public SpeechManager(SpeechRecognizer speechRecognizer, SpeechSynthesizer speechSynthesizer)
    {
        this.speechRecognizer = speechRecognizer;
        this.speechSynthesizer = speechSynthesizer;
    }

    public async Task<SpeechSynthesisResult> SpeakAsync(Message message)
    {
        Console.WriteLine($"\"{message.Content}\"");
        var result = await speechSynthesizer.SpeakTextAsync(message.Content);
        return result;
    }

    public Message? OutputSpeechSynthesisResult(SpeechSynthesisResult speechSynthesisResult, Message message)
    {
        Console.WriteLine($"Assistant stopped speaking because {speechSynthesisResult.Reason}");
        switch (speechSynthesisResult.Reason)
        {
            case ResultReason.SynthesizingAudioCompleted:
                return null;
            case ResultReason.Canceled:
                if (message.Content.Length > 10)
                {
                    message.Content = message.Content.Substring(0, 10) + "—";
                }
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                if (cancellation.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                    Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                }
                return new Message {
                    Role = Role.System,
                    Content = $"The Client interrupted."
                };
            default:
                throw new NotImplementedException();
        }
    }
}
