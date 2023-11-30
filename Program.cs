// TODO Create "Reminders"--Reminders cause a System Message, which can prompt a follow-up Chat Completion.
// TODO Break out the Pinned message, each paragraph to be controlled by a system component
// Explore switching to Maui (no current solution for sound effects in console app)
// TODO Add Calendar service (Ical.Net)
// TODO Add Hue lights service
// TODO Add Users service (JSON)
// TODO Add Contacts service (JSON)

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
    private static ClientTaskService? clientTaskService;
    private static ReminderService? reminderService;
    private static SettingsManager? settingsManager;
    private static SpeechConfig? speechConfig;
    private static SpeechSynthesizer? speechSynthesizer;
    private static AudioConfig? audioConfig;
    private static SpeechManager? speechManager;
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
        clientTaskService = await ClientTaskService.CreateAsync(new CancellationTokenSource().Token);

        // Reminders
        reminderService = await ReminderService.CreateAsync(new CancellationTokenSource().Token);

        // Settings
        settingsManager = await SettingsManager.CreateInstance(new SettingConfig[] {
            ClientSoundDeviceSetting.SettingConfig,
            GptModelSetting.SettingConfig,
            InteractionModeSetting.SettingConfig
        }, new CancellationTokenSource().Token);
        var soundDeviceSettingGetter = settingsManager.GetterFor<ClientSoundDeviceSetting, SoundDeviceTypes>();
        var gptModelSettingGetter = settingsManager.GetterFor<GptModelSetting, GptModel>();
        var interactionModeSettingGetter = settingsManager.GetterFor<InteractionModeSetting, InteractionMode>();

        // MS Speech Service Config
        speechConfig = SpeechConfig.FromSubscription(config["SPEECH_KEY"], config["SPEECH_REGION"]);
        speechConfig.SpeechSynthesisVoiceName = JustStrings.VOICE_NAME;
        speechConfig.SpeechRecognitionLanguage = "en-US";
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();

        // Transcription
        transcriptionService = new CloudTranscriptionService(speechConfig, audioConfig, settingsManager);
        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await transcriptionService.StopTranscriptionAsync();
        };

        // Speech
        speechSynthesizer = new SpeechSynthesizer(speechConfig);
        speechManager = new SpeechManager(transcriptionService, speechSynthesizer, settingsManager);
        transcriptionService.Recognizing += () =>
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
                clientTaskService,
                reminderService,
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
                clientTaskService,
                reminderService,
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

        Console.WriteLine("Speak into your microphone.");
        transcriptionService.StartTranscriptionAsync();

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
