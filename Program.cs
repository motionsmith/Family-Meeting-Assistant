// Make a message capable of not triggering a completion response even in Converse mode.
// TODO Break out the Pinned message, each paragraph to be controlled by a system component
// - Ability for assistant to modify their own prompt. (requires retreival)
// - Ability for assistant to choose from a set of personalities 
// - Allows for tools to own their pinned prompt instructions
// TODO Use ReminderService to create a similar service that can drive a story (e.g. water level is rising). 
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
    private static SoundController? soundChatObserver;
    private static readonly TimeMessageProvider timeMessageProvider = new TimeMessageProvider();
    private static NewsMessageProvider? newsMessageProvider;
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
        // News Headlines
        newsMessageProvider = new NewsMessageProvider(config["NEWSAPI_API_KEY"]);

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
                newsMessageProvider,
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

        // Sound Controller
        soundChatObserver = new SoundController(speechSynthesizer, transcriptionService, chatCompleter);

        // Chat management
        chatManager = await ChatManager.CreateAsync(
            new List<IChatObserver>() {
                speechManager,
                consoleChatObserver,
                soundChatObserver,
                circumstanceManager,
                chatCompleter
            },
            new IMessageProvider[] {
                transcriptionService,
                weatherMessageProvider,
                timeMessageProvider,
                newsMessageProvider,
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

        Console.WriteLine("Speak into your microphone or type your message and press Enter:");
        // Start audio transcription (if available)
        transcriptionService.StartTranscriptionAsync();

        // Run continuous update loop and text input loop indefinitely
        await Task.WhenAll(
            chatManager.StartContinuousUpdatesAsync(),
            Task.Run(async () =>
            {
                while (true)
                {
                    var input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input))
                        continue;
                    chatManager.AddUserMessage(input);
                }
            })
        );

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
