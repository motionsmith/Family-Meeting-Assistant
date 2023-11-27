    // TODO OpenAIApi to retry during timeouts. Necessary in passive mode.
    // TODO Keyboard Button to manually trigger a chat completion request
    // TODO Keyboard Button Start/Stop dictation listening
    
    // Three interaction modes
    /*
    1. **Active Mode** – The AI can listen and respond without needing a wake word. Can still be used passively by telling it how to behave.
    2. **Mute AI Mode** – The AI listens passively and only responds when the wake word is used. This is like Active mode except the dictation message is not enough for the AI to respond. The message must also contain the wake word. Will be effective at reducing model API consts.
    3. **Mute User Mode** – The AI does not listen or respond until it is reactivated, possibly through a different interface or command. This is how Google Home works
    */
    // TODO Write prompts for the AI to teach the user about these modes.

    // TODO Make tasks completable by task index (or line number). This will be more reliable.
    // TODO Evaluate ways of reducing chat history length to ~30 messages. E.g. When 30 chats accumulate, use a GPT to summarize them into one message.
    // TODO Add a tool setting for Client name.

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
    private static SettingsManager? settingsManager;
    private static SpeechConfig? speechConfig;
    private static SpeechSynthesizer? speechSynthesizer;
    private static AudioConfig? audioConfig;
    private static SpeechManager? speechManager;
    private static SpeechRecognizer? speechRecognizer;
    private static DictationMessageProvider? dictationMessageProvider;
    private static CircumstanceManager? circumstanceManager;
    private static ChatManager? chatManager;

    async static Task Main(string[] args)
    {
        // User secrets
        config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        // Weather
        weatherMessageProvider = new WeatherMessageProvider(config["OWM_KEY"], () => new(Lat, Long));

        // Task list
        await ChoreManager.LoadAsync(new CancellationTokenSource().Token);

        // Settings
        settingsManager = await SettingsManager.CreateInstance(new SettingConfig[] {
            ClientSoundDeviceSetting.SettingConfig,
            GptModelSetting.SettingConfig,
            InteractionModeSetting.SettingConfig,
        }, new CancellationTokenSource().Token);
        var soundDeviceSettingGetter = settingsManager.GetterFor<ClientSoundDeviceSetting, SoundDeviceTypes>();
        var gptModelSettingGetter = settingsManager.GetterFor<GptModelSetting, GptModel>();
        var interactionModeSettingGetter = settingsManager.GetterFor<InteractionModeSetting, InteractionMode>();

        // Speech
        speechConfig = SpeechConfig.FromSubscription(config["SPEECH_KEY"], config["SPEECH_REGION"]);
        speechConfig.SpeechSynthesisVoiceName = JustStrings.VOICE_NAME;
        speechConfig.SpeechRecognitionLanguage = "en-US";
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        speechSynthesizer = new SpeechSynthesizer(speechConfig);
        speechManager = new SpeechManager(speechRecognizer, speechSynthesizer, soundDeviceSettingGetter);
        var _ = KeyboardSpeechInterrupter.StartKeyboardInterrupter(speechManager);

        // Dictation
        dictationMessageProvider = new DictationMessageProvider(speechRecognizer, speechSynthesizer);
        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await dictationMessageProvider.StopContinuousRecognitionAsync();
        };
        _ = dictationMessageProvider.StartContinuousRecognitionAsync();

        // Circumstances (prompt state)
        circumstanceManager = await CircumstanceManager.CreateAsync(new Circumstance[] {
            new SmithsonianDefaultCircumstance(
                weatherMessageProvider,
                settingsManager)
        },
        (msg) => { chatManager.PinnedMessage = msg; },
        new CancellationTokenSource().Token);

        // Chat management
        chatManager = await ChatManager.CreateAsync(
            new List<IChatObserver>() {
                speechManager,
                consoleChatObserver,
                circumstanceManager
            },
            new IMessageProvider[] {
                dictationMessageProvider,
                weatherMessageProvider,
                timeMessageProvider,
                settingsManager,
                speechManager
            },
            toolsDel: () => circumstanceManager.Tools,
            gptModelSettingGetter,
            interactionModeSettingGetter,
            new CancellationTokenSource().Token);
        
        Console.WriteLine("Speak into your microphone.");
        await chatManager.StartContinuousUpdatesAsync();
    }
}
