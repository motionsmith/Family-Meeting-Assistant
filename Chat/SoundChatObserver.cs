using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;

public class SoundController : IChatObserver
{
    private SpeechSynthesizer _speechSynthesizer;
    private CloudTranscriptionService _transcription;
    private IChatCompleter _chatCompleter;

    public SoundController(SpeechSynthesizer speechSynthesizer, CloudTranscriptionService transcription, IChatCompleter chatCompleter)
    {
        _speechSynthesizer = speechSynthesizer;
        _transcription = transcription;
        _chatCompleter = chatCompleter;
        _transcription.Recognizing += HandleTranscriptionRecognizing;
        _transcription.Recognized += HandleTranscriptionRecognized;
        _transcription.SessionStarted += HandleTranscriptionSessionStarted;
        _transcription.SessionStopped += HandleTranscriptionSessionStopped;
        _speechSynthesizer.SynthesisStarted += HandleSynthesisStarted;
        _speechSynthesizer.Synthesizing += HandleSynthesizing;
        _speechSynthesizer.SynthesisCompleted += HandleSynthesisCompleted;
        _chatCompleter.ChatCompletionRequested += HandleChatCompletionRequeted;
    }

    private async void HandleChatCompletionRequeted()
    {
        await PlaySound("chat-completion-requested.wav");
    }

    private async void HandleTranscriptionSessionStopped()
    {
        await PlaySound("transcription-session-stopped.wav");
    }

    private async void HandleTranscriptionSessionStarted()
    {
        await PlaySound("transcription-session-started.wav");
    }

    private async void HandleTranscriptionRecognized()
    {
       //await PlaySound("transcription-recognized.wav");
    }

    private async void HandleTranscriptionRecognizing()
    {
        await PlaySound("transcription-recognizing.wav");
    }

    private async void HandleSynthesisStarted(object? sender, SpeechSynthesisEventArgs e)
    {
        //await PlaySound("synthesis-started.wav");
    }

    private async void HandleSynthesizing(object? sender, SpeechSynthesisEventArgs e)
    {
        //await PlaySound("synthesizing.wav");
    }

    private async void HandleSynthesisCompleted(object? sender, SpeechSynthesisEventArgs e)
    {
        //await PlaySound("synthesis-completed.wav");
    }

    public void OnNewMessages(IEnumerable<Message> messages)
    {
        var soundMessages = messages.Where(m => string.IsNullOrEmpty(m.MessageSound) == false);
        // DEBUG
        foreach (var m in soundMessages)
        {
            PlaySound(m.MessageSound);
        }
    }

    private static async Task PlaySound(string filePath)
    {
        using (var process = Process.Start(new ProcessStartInfo
        {
            FileName = "afplay",
            Arguments = filePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = "Assets/Sounds"
        }))
        {
            try
            {
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}