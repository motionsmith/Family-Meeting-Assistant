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
    private static SpeechManager? speechManager;
    private static SpeechRecognizer? speechRecognizer;
    private static ChatManager chatManager = new();
    private static OpenAIApi openAIApi = new();
    private static OpenWeatherMapClient? openWeatherMapClient;
    private static CircumstanceManager? circumstanceManager;
    private static TimeSpan loopMinDuration = TimeSpan.FromMilliseconds(100);

    // TODO Terminate OPENAI requests if dictation is recognized.
    // TODO Cannot send an API request while speech is being recognized, 
    // TODO After an interruption, cannot send out another comption request until some pentalty duration.
    // TODO Support for both Airpods and open air mode. Open Air mode is required to silence the mic while assistant is talking.
    // TODO During open air mode, use a keyboard button or something to be able to interrupt the speech synthesis.
    // TODO Silent mode - only requests chat completion after a wake word
    // TODO Text input mode

    // Three interaction modes
    /*
    1. **Active Mode** – The AI can listen and respond without needing a wake word.
    2. **Wake Word Mode** – The AI listens passively and only responds when the wake word is used.
    3. **Silent Mode** – The AI does not listen or respond until it is reactivated, possibly through a different interface or command.
    */

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
        dictationMessageProvider = new DictationMessageProvider(speechRecognizer, speechSynthesizer);
        speechManager = new SpeechManager(speechRecognizer, speechSynthesizer);

        openWeatherMapClient = new OpenWeatherMapClient(config["OWM_KEY"], () => new(Lat, Long));
        circumstanceManager = new CircumstanceManager(openWeatherMapClient);

        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await dictationMessageProvider.StopContinuousRecognitionAsync();
        };

        var tkn = new CancellationTokenSource().Token;
        await ChoreManager.LoadAsync(tkn);
        await circumstanceManager.LoadStateAsync(tkn);
        await chatManager.LoadAsync(tkn);

        chatManager.PinnedMessage = circumstanceManager.PinnedMessage;

        // The system message that gets added for each app launch
        chatManager.Messages.Add(circumstanceManager.PlayerJoinedMessage);

        // This message will allow the Assistant to have the first word.
        if (false) await TryCompleteChat(false, false, tkn);

        Console.WriteLine("Speak into your microphone.");
        await dictationMessageProvider.StartContinuousRecognitionAsync();
        while (true)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            var newUserDictatedMessages = await dictationMessageProvider.GetNewMessagesAsync(tkn);
            if (newUserDictatedMessages.Count() > 0)
            {
                foreach (var message in newUserDictatedMessages)
                {
                    Console.WriteLine($"Heard \"{message.Content}\"");
                }
                chatManager.AddMessages(await TimeMessenger.GetNewMessagesAsync(tkn));
                chatManager.AddMessages(newUserDictatedMessages);
                Console.WriteLine($"[System] Waiting for response...");
                await TryCompleteChat(true, true, tkn);
            }
            stopwatch.Stop();
            if (stopwatch.Elapsed < loopMinDuration)
            {
                await Task.Delay(loopMinDuration - stopwatch.Elapsed);
            }
        }
    }

    private static async Task TryCompleteChat(bool allowRecursion, bool allowTools, CancellationToken tkn)
    {
        Message assistantMessage;
        try
        {
            assistantMessage = await openAIApi.GetChatCompletionAsync(chatManager.ChatCompletionRequestMessages, tkn, allowTools ? circumstanceManager.Tools : null);
        }
        catch (Exception ex)
        {
            assistantMessage = new Message
            {
                Role = Role.System,
                Content = ex.Message
            };
        }
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

    private static async Task<IEnumerable<Message>> HandleAssistantMessage(Message message, CancellationToken cancelToken)
    {
        var results = new List<Message>();

        // Handle speaking
        bool assistantSpoke = false;
        bool calledTools = message.ToolCalls != null && message.ToolCalls.Count > 0;
        if (message.Content != null)
        {
            var speechResult = await speechManager.SpeakAsync(message);
            var speechResultMessage = speechManager.OutputSpeechSynthesisResult(speechResult, message);
            if (speechResultMessage != null)
            {
                Console.WriteLine($"Assistant was interrupted.");
                results.Add(speechResultMessage);
                chatManager.AddMessage(speechResultMessage);
            }
            assistantSpoke = speechResult.Reason == ResultReason.SynthesizingAudioCompleted;
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
