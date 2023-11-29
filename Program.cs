// TODO Mute User Mode – The Client can ask to be muted. Assistant mutes The Client, then Assistant does not listen or respond until it is reactivated when The Client says "unmute".
// TODO Add Users service
// TODO Add Contacts service
// TODO Add Calendar service
// TODO Add Hue lights service
// Explore switching to Maui (no current solution for sound effects in console app)

using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;

class Program
{
    private static readonly double Lat = 47.5534058;
    private static readonly double Long = -122.3093843;
    private static readonly ConsoleChatObserver consoleChatObserver = new ConsoleChatObserver();
    private static readonly TimeMessageProvider timeMessageProvider = new TimeMessageProvider();
    private static IConfiguration? config;
    private static WeatherMessageProvider? weatherMessageProvider;
    private static ClientTaskManager? taskManager;
    private static SettingsManager? settingsManager;
    private static SpeechConfig? speechConfig;
    private static SpeechSynthesizer? speechSynthesizer;
    private static AudioConfig? audioConfig;
    private static SpeechManager? speechManager;
    private static SpeechRecognizer? speechRecognizer;
    private static CloudTranscriptionService? transcriptionService;
    private static CircumstanceManager? circumstanceManager;
    private static ChatManager? chatManager;
    private static OpenAiChatCompleter? chatCompleter;

    async static Task Main(string[] args)
    {
        // User secrets
        config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        // Weather
        weatherMessageProvider = new WeatherMessageProvider(config["OWM_KEY"], () => new(Lat, Long));

        // Task list
        taskManager = await ClientTaskManager.CreateAsync(new CancellationTokenSource().Token);

        // Settings
        settingsManager = await SettingsManager.CreateInstance(new SettingConfig[] {
            ClientSoundDeviceSetting.SettingConfig,
            GptModelSetting.SettingConfig,
            InteractionModeSetting.SettingConfig,
            TranscribeSetting.SettingConfig
        }, new CancellationTokenSource().Token);
        var soundDeviceSettingGetter = settingsManager.GetterFor<ClientSoundDeviceSetting, SoundDeviceTypes>();
        var gptModelSettingGetter = settingsManager.GetterFor<GptModelSetting, GptModel>();
        var interactionModeSettingGetter = settingsManager.GetterFor<InteractionModeSetting, InteractionMode>();
        var transcribeSettingGetter = settingsManager.GetterFor<TranscribeSetting, bool>();

        // MS Speech Service Config
        speechConfig = SpeechConfig.FromSubscription(config["SPEECH_KEY"], config["SPEECH_REGION"]);
        speechConfig.SpeechSynthesisVoiceName = JustStrings.VOICE_NAME;
        speechConfig.SpeechRecognitionLanguage = "en-US";
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();

        // Transcription
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        transcriptionService = new CloudTranscriptionService(speechRecognizer);
        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await transcriptionService.StopTranscriptionAsync(false);
        };

        // Speech
        speechSynthesizer = new SpeechSynthesizer(speechConfig);
        speechManager = new SpeechManager(transcriptionService, speechSynthesizer, settingsManager);
        speechRecognizer.Recognizing += (s, e) =>
        {
            if (speechManager.IsSynthesizing)
            {
                speechManager.InterruptSynthesis();
            }
        };

        // Circumstances (prompt state)
        circumstanceManager = await CircumstanceManager.CreateAsync(new Circumstance[] {
            new SmithsonianDefaultCircumstance(
                weatherMessageProvider,
                settingsManager,
                taskManager,
                timeMessageProvider)
        },
        (msg) => { chatManager.PinnedMessage = msg; },
        new CancellationTokenSource().Token);

        // Chat Completion
        chatCompleter = new OpenAiChatCompleter(
            toolsDel: () => circumstanceManager.Tools,
            messagesDel: () => chatManager.Messages,
            settingsManager);

        // Chat management
        chatManager = await ChatManager.CreateAsync(
            new List<IChatObserver>() {
                speechManager,
                consoleChatObserver,
                circumstanceManager,
                chatCompleter
            },
            new IMessageProvider[] {
                transcriptionService,
                weatherMessageProvider,
                timeMessageProvider,
                settingsManager,
                speechManager,
                taskManager,
                chatCompleter
            },
            new CancellationTokenSource().Token);

        // User Commands
        UserCommands.Interrupt += () =>
        {
            if (speechManager.IsSynthesizing)
            {
                speechManager.InterruptSynthesis();
            }
            else
            {
                Console.WriteLine($"[Debug] The Client interrupts while the assistant is silent.");
            }
        };
        UserCommands.RequestChatCompletion += () =>
        {
            if (chatCompleter.IsCompletionTaskRunning || speechManager.IsSynthesizing)
                return;
            chatCompleter.RequestChatCompletion();
        };
        UserCommands.ToggleTranscription += () =>
        {
            if (transcribeSettingGetter.Invoke())
            {
                transcriptionService.StopTranscriptionAsync(true);
            }
            else
            {
                transcriptionService.StartTranscriptionAsync(true);
            }
        };

        Console.WriteLine("Speak into your microphone.");
        transcriptionService.StartTranscriptionAsync(false);

        var longTasks = new List<Task> {
            chatManager.StartContinuousUpdatesAsync(),
            UserCommands.StartReadingAsync()
        };

        var allLongTasks = Task.WhenAny(longTasks);
        await allLongTasks;

        Console.WriteLine("Goodbye");
        await Task.Delay(5000);
    }

    private static void Test(Task<Task> task)
    {
        if (task.Exception != null)
        {
            Console.WriteLine($"[Debug] {task.Exception.Message}");
        }
    }
}
