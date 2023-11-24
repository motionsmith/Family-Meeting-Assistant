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
    private static CircumstanceManager circumstanceManager = new ();
    private static bool IS_SPEECH_TO_TEXT_WORKING = true;
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
        dictationMessageProvider = new DictationMessageProvider(speechRecognizer);
        speechManager = new SpeechManager(speechRecognizer, speechSynthesizer, JustStrings.ASSISTANT_NAME);

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
        var assistantJoinMessage = await openAIApi.GetChatCompletionAsync(chatManager.ChatCompletionRequestMessages, tkn);
        chatManager.AddMessage(assistantJoinMessage);
        if (string.IsNullOrEmpty(assistantJoinMessage.Content) == false)
        {
            await speechManager.Speak(assistantJoinMessage.Content, tkn, IS_SPEECH_TO_TEXT_WORKING);
        }
        await circumstanceManager.UpdateCurrentCircumstance(assistantJoinMessage, tkn);
        Console.WriteLine("Speak into your microphone.");
        while (true)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var newMessages = await dictationMessageProvider.GetNewMessagesAsync(tkn);
            if (newMessages.Count() > 0)
            {
                chatManager.AddMessages(newMessages);
                Console.WriteLine($"[System] Waiting for response...");
                Message toolCallMessage;
                try
                {
                    toolCallMessage = await openAIApi.GetToolCallAsync(chatManager.ChatCompletionRequestMessages, tkn, circumstanceManager.Tools);
                }
                catch (Exception ex)
                {
                    toolCallMessage = new Message
                    {
                        Role = Role.System,
                        Content = ex.Message
                    };
                }
                chatManager.AddMessage(toolCallMessage);
                var toolMessages = await HandleToolCalls(toolCallMessage, tkn);
                await circumstanceManager.UpdateCurrentCircumstance(toolCallMessage, tkn);
                chatManager.PinnedMessage = circumstanceManager.PinnedMessage;
                await chatManager.SaveAsync(tkn);
                var toolCallsRequireFollowUp = toolMessages.Any(msg => msg.Role == Role.Tool && msg.FollowUp);
                if (toolCallsRequireFollowUp)
                {
                    var toolCallAssistantResponseMessage = await openAIApi.GetChatCompletionAsync(chatManager.ChatCompletionRequestMessages, tkn);
                    chatManager.AddMessage(toolCallAssistantResponseMessage);
                    await circumstanceManager.UpdateCurrentCircumstance(toolCallAssistantResponseMessage, tkn);
                    chatManager.PinnedMessage = circumstanceManager.PinnedMessage;
                    await speechManager.Speak(toolCallAssistantResponseMessage.Content, tkn, IS_SPEECH_TO_TEXT_WORKING);
                }
            }
            stopwatch.Stop();
            if (stopwatch.Elapsed < loopMinDuration)
            {
                await Task.Delay(loopMinDuration - stopwatch.Elapsed);
            }
        }
    }

    private static async Task<IEnumerable<Message>> HandleToolCalls(Message message, CancellationToken cancelToken)
    {
        if (string.IsNullOrEmpty(message.Content) == false)
        {
            await speechManager.Speak(message.Content, cancelToken, IS_SPEECH_TO_TEXT_WORKING);
        }

        if (message.ToolCalls == null || message.ToolCalls.Count == 0)
        {
            return new List<Message>();
        }

        var results = new List<Message>();
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
