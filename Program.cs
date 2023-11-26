using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;

class Program
{
    public static double Lat = 47.5534058;
    public static double Long = -122.3093843;

    private static IConfiguration? config;

    private static SpeechConfig? speechConfig;
    private static SpeechSynthesizer? speechSynthesizer;
    private static AudioConfig? audioConfig;
    private static DictationMessageProvider? dictationMessageProvider;
    private static TimeMessageProvider timeMessageProvider = new TimeMessageProvider();
    private static WeatherMessageProvider? weatherMessageProvider;
    private static SpeechManager? speechManager;
    private static SpeechRecognizer? speechRecognizer;
    private static ChatManager chatManager = new();
    private static OpenAIApi openAIApi = new();
    private static CircumstanceManager? circumstanceManager;
    private static MessageProviderManager? messageProviderManager;
    private static SettingsManager? settingsManager;
    private static TimeSpan loopMinDuration = TimeSpan.FromMilliseconds(100);

    // TODO Add tool setting to change the gpt model
    // TODO Evaluate ways of reducing chat history length to ~30 messages. E.g. When 30 chats accumulate, use a GPT to summarize them into one message.
    // TODO Add a setting for Client name.

    // Three interaction modes

    /*
    1. **Active Mode** – The AI can listen and respond without needing a wake word. Can still be used passively by telling it how to behave.
    2. **Mute AI Mode** – The AI listens passively and only responds when the wake word is used. This is like Active mode except the dictation message is not enough for the AI to respond. The message must also contain the wake word. Will be effective at reducing model API consts.
    3. **Mute User Mode** – The AI does not listen or respond until it is reactivated, possibly through a different interface or command. This is how Google Home works
    */
    // TODO Write prompts for the AI to teach the user about these modes.



    async static Task Main(string[] args)
    {
        config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        // Speech stuff has to be configured in Main
        speechConfig = SpeechConfig.FromSubscription(config["SPEECH_KEY"], config["SPEECH_REGION"]);
        speechConfig.SpeechSynthesisVoiceName = JustStrings.VOICE_NAME;
        speechConfig.SpeechRecognitionLanguage = "en-US";
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        speechSynthesizer = new SpeechSynthesizer(speechConfig);

        // Settings
        settingsManager = await SettingsManager.CreateInstance(new SettingConfig[] {
            ClientSoundDeviceSetting.SettingConfig
        }, new CancellationTokenSource().Token);
        var soundDeviceSettingGetter = settingsManager.GetterFor<ClientSoundDeviceSetting, SoundDeviceTypes>();

        dictationMessageProvider = new DictationMessageProvider(speechRecognizer, speechSynthesizer);
        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await dictationMessageProvider.StopContinuousRecognitionAsync();
        };
        speechManager = new SpeechManager(speechRecognizer, speechSynthesizer, soundDeviceSettingGetter);
        KeyboardSpeechInterrupter.StartKeyboardInterrupter(speechManager, new CancellationTokenSource().Token);
        weatherMessageProvider = new WeatherMessageProvider(config["OWM_KEY"], () => new(Lat, Long));




        circumstanceManager = new CircumstanceManager(new Circumstance[] {
            new SmithsonianDefaultCircumstance(
                weatherMessageProvider,
                settingsManager)
        });

        messageProviderManager = new MessageProviderManager(new IMessageProvider[] {
            dictationMessageProvider,
            weatherMessageProvider,
            timeMessageProvider,
            settingsManager,
            speechManager
        });
        var cts = new CancellationTokenSource();
        var tkn = cts.Token;
        await ChoreManager.LoadAsync(tkn);
        await circumstanceManager.LoadStateAsync(tkn);
        await chatManager.LoadAsync(tkn);

        chatManager.PinnedMessage = circumstanceManager.PinnedMessage;

        Console.WriteLine("Speak into your microphone.");
        await dictationMessageProvider.StartContinuousRecognitionAsync();
        while (true)
        {
            try
            {
                tkn = cts.Token;
                Stopwatch stopwatch = Stopwatch.StartNew();

                var allNewMessages = await messageProviderManager.GetNewMessagesAsync(cts);
                chatManager.AddMessages(allNewMessages);
                if (allNewMessages.Count() > 0)
                {
                    // DEBUG
                    foreach (var message in allNewMessages.Where(m => m.Role == Role.User))
                    {
                        Console.WriteLine($"[Mic] \"{message.Content}\"");
                    }
                    foreach (var message in allNewMessages.Where(m => m.Role == Role.System))
                    {
                        Console.WriteLine($"[System] {message.Content}");
                    }
                    await TryCompleteChat(true, true, tkn);
                }
                stopwatch.Stop();
                if (stopwatch.Elapsed < loopMinDuration)
                {
                    await Task.Delay(loopMinDuration - stopwatch.Elapsed, tkn);
                }
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"Loop cancelled. Enforcing 1s loop delay");
                await chatManager.SaveAsync(new CancellationTokenSource().Token);
                await Task.Delay(1000);
                cts = new CancellationTokenSource();
            }

        }
    }

    private static async Task TryCompleteChat(bool allowRecursion, bool allowTools, CancellationToken tkn)
    {
        Console.WriteLine($"[Debug] Waiting for response...");

        Message? assistantMessage = null;
        try
        {
            assistantMessage = await openAIApi.GetChatCompletionAsync(chatManager.ChatCompletionRequestMessages, tkn, allowTools ? circumstanceManager.Tools : null);
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"[Debug] ChatCompletion request was cancelled.");
        }
        catch (Exception ex)
        {
            assistantMessage = new Message
            {
                Role = Role.System,
                Content = ex.Message
            };
        }

        if (assistantMessage != null) // Null occurs during TaskCancelledException
        {
            var toolMessages = await HandleAssistantMessage(assistantMessage, tkn);
            await circumstanceManager.UpdateCurrentCircumstance(assistantMessage, tkn);
            chatManager.PinnedMessage = circumstanceManager.PinnedMessage;
            await chatManager.SaveAsync(tkn);
            if (allowRecursion)
            {
                var toolCallsRequireFollowUp = toolMessages.Any(msg => msg.Role == Role.Tool && msg.FollowUp);
                if (toolCallsRequireFollowUp)
                {
                    await TryCompleteChat(false, false, tkn);
                }
            }
        }

    }

    private static async Task<IEnumerable<Message>> HandleAssistantMessage(Message message, CancellationToken cancelToken)
    {
        var results = new List<Message>();

        // Handle speaking
        bool calledTools = message.ToolCalls != null && message.ToolCalls.Count > 0;
        if (message.Content != null)
        {
            var speechResult = await speechManager.SpeakAsync(message);
        }

        // Add message
        chatManager.AddMessage(message);

        // Handle tool calling
        if (calledTools == false)
        {
            return new List<Message>();
        }

        foreach (var call in message.ToolCalls)
        {
            var functionName = call.Function.Name;
            var arguments = call.Function.Arguments;
            Console.WriteLine($"[{JustStrings.ASSISTANT_NAME}] {functionName}({arguments})");
            Message toolMessage;
            var tool = circumstanceManager.Tools.FirstOrDefault(tool => tool.Function.Name == functionName);
            if (tool != null)
            {
                toolMessage = await tool.Execute(call, cancelToken);
            }
            else
            {
                // Handle unknown function
                toolMessage = new Message
                {
                    Content = $"Unknown tool function name {functionName}. Tool call failed.",
                    ToolCallId = call.Id,
                    Role = Role.Tool
                };
            }
            results.Add(toolMessage);
            chatManager.AddMessage(toolMessage);
        }
        return results;
    }
}
