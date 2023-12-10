using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

public class CloudTranscriptionService : IMessageProvider, ITranscriptionService
{
    private enum RecognitionMode
    {
        Off,
        Keyword,
        Continuous
    }

    public event Action Recognizing;
    public event Action Recognized;
    public event Action SessionStarted;
    public event Action SessionStopped;

    private SpeechRecognizer speechRecognizerContinuous;
    private SpeechRecognizer speechRecognizerKeyword;
    private SpeechRecognizer? currentSpeechRecognizer;
    private ConcurrentQueue<Message> messageQueue = new ConcurrentQueue<Message>();
    private Func<InteractionMode> interactionModeGetter;
    private KeywordRecognitionModel keywordModel;
    public CloudTranscriptionService(SpeechConfig speechConfig, AudioConfig audioConfig, SettingsManager settingsManager)
    {
        this.speechRecognizerContinuous = new SpeechRecognizer(speechConfig, audioConfig);
        this.speechRecognizerKeyword = new SpeechRecognizer(speechConfig, audioConfig);
        keywordModel = KeywordRecognitionModel.FromFile("Assets/keyword.table");
        this.interactionModeGetter = settingsManager.GetterFor<InteractionModeSetting, InteractionMode>();

        speechRecognizerContinuous.Recognizing += OnSpeechRecognizing;
        speechRecognizerKeyword.Recognizing += OnSpeechRecognizing;
        speechRecognizerContinuous.Recognized += OnSpeechRecognized;
        speechRecognizerKeyword.Recognized += OnSpeechRecognized;
        speechRecognizerContinuous.Canceled += OnSpeechCanceled;
        speechRecognizerKeyword.Canceled += OnSpeechCanceled;
        speechRecognizerContinuous.SessionStarted += OnSpeechSessionStarted;
        speechRecognizerKeyword.SessionStarted += OnSpeechSessionStarted;
        speechRecognizerContinuous.SessionStopped += OnSpeechSessionStopped;
        speechRecognizerKeyword.SessionStopped += OnSpeechSessionStopped;
    }

    private void OnSpeechRecognizing(object? sender, SpeechRecognitionEventArgs e)
    {
        Recognizing?.Invoke();
    }

    public async Task StartTranscriptionAsync()
    {
        try
        {
            if (interactionModeGetter.Invoke() == InteractionMode.Ignore)
            {
                Console.WriteLine($"[Transcription] Start keyword recognition");
                currentSpeechRecognizer = speechRecognizerKeyword;
                await currentSpeechRecognizer.StartKeywordRecognitionAsync(keywordModel);
            }
            else
            {
                Console.WriteLine($"[Transcription] Start continuous recognition");
                currentSpeechRecognizer = speechRecognizerContinuous;
                await currentSpeechRecognizer.StartContinuousRecognitionAsync();
            }
            
            Console.WriteLine($"[Transcription] Recognition start task completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            currentSpeechRecognizer = null;
        }
    }

    public async Task StopTranscriptionAsync()
    {
        try
        {
            
            Console.WriteLine($"[Transcription] Stopping recognition...");
            if (currentSpeechRecognizer != null)
            {
                if (currentSpeechRecognizer == speechRecognizerKeyword)
                {
                    await currentSpeechRecognizer.StopKeywordRecognitionAsync();
                }
                else
                {
                    await currentSpeechRecognizer.StopContinuousRecognitionAsync();
                }
                
                Console.WriteLine($"[Transcription] Stopped recoginition");
            }
        }
        finally
        {
            currentSpeechRecognizer = null;
        }
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var newMessages = messageQueue.ToArray(); // Garbage
        messageQueue.Clear();
        return newMessages;
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
            Console.WriteLine($"RECOGNIZED: ");
            var recognitionMessage = new Message
            {
                Content = e.Result.Text,
                Role = Role.User
            };
            messageQueue.Enqueue(recognitionMessage);
            Recognized?.Invoke();
        }
        else if (e.Result.Reason == ResultReason.NoMatch)
        {
            Console.WriteLine($"NOMATCH: Speech could not be recognized.");
        }
    }

    private void OnSpeechCanceled(object? sender, SpeechRecognitionCanceledEventArgs e)
    {
        Console.WriteLine($"CANCELED: Reason={e.Reason}");

        if (e.Reason == CancellationReason.Error)
        {
            Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
            Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
            Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
        }
    }

    private void OnSpeechSessionStarted(object? sender, SessionEventArgs e)
    {
        Console.WriteLine("Session Started");
        SessionStarted?.Invoke();
        
    }

    private void OnSpeechSessionStopped(object? sender, SessionEventArgs e)
    {
        Console.WriteLine("Session Stopped");
        SessionStopped?.Invoke();
    }
}
