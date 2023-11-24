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
    private static CircumstanceManager circumstanceManager = new();
    private static bool IS_SPEECH_TO_TEXT_WORKING = false;
    private static TimeSpan loopMinDuration = TimeSpan.FromMilliseconds(20);

    async static Task Main(string[] args)
    {
        config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        // Speech stuff has to be configured in Main
        speechConfig = SpeechConfig.FromSubscription(config["SPEECH_KEY"], config["SPEECH_REGION"]);
        speechConfig.SpeechRecognitionLanguage = "en-US";
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        speechSynthesizer = new SpeechSynthesizer(speechConfig);
        dictationMessageProvider = new DictationMessageProvider(speechRecognizer, speechSynthesizer);
        speechManager = new SpeechManager(speechRecognizer, speechSynthesizer);

        openWeatherMapClient = new OpenWeatherMapClient(config["OWM_KEY"], () => new(Lat, Long));


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
        await TryCompleteChat(false, false, tkn);

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
                    Console.ResetColor();
                }
                
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
