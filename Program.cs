using System.Diagnostics;
using System.Reflection.Metadata;
using Family_Meeting_Assistant;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    public static double Lat = 47.5534058;
    public static double Long = -122.3093843;

    private static readonly string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
    private static readonly string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
    private static readonly string assistantName = Environment.GetEnvironmentVariable("ASSISTANT_NAME");
    private static readonly string owmKey = Environment.GetEnvironmentVariable("OWM_KEY");

    private static SpeechConfig? speechConfig;
    private static SpeechSynthesizer? speechSynthesizer;
    private static AudioConfig? audioConfig;
    private static DictationMessageProvider? dictationMessageProvider;
    private static SpeechManager? speechManager;
    private static SpeechRecognizer? speechRecognizer;
    private static ChatManager chatManager = new();
    private static OpenAIApi openAIApi = new();
    private static OpenWeatherMapClient openWeatherMapClient = new OpenWeatherMapClient(owmKey, () => new(Lat, Long));
    private static CircumstanceManager circumstanceManager = new ();
    private static bool IS_SPEECH_TO_TEXT_WORKING = true;

    async static Task Main(string[] args)
    {
        // Speech stuff has to be configured in Main
        speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        speechSynthesizer = new SpeechSynthesizer(speechConfig);
        dictationMessageProvider = new DictationMessageProvider(speechRecognizer);
        speechManager = new SpeechManager(speechRecognizer, speechSynthesizer, assistantName);

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
            var newMessages = await dictationMessageProvider.GetNewMessagesAsync(tkn);
            if (newMessages.Count() > 0)
            {
                chatManager.AddMessages(newMessages);
                Console.WriteLine($"[System] Waiting for Assistant response...");
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
            Console.WriteLine($"[{assistantName}] {functionName}({arguments})");
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
