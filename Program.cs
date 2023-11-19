using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Newtonsoft.Json.Linq;

class Program
{
    // This example requires environment variables named "SPEECH_KEY" and "SPEECH_REGION"
    private static readonly string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
    private static readonly string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
    private static readonly string assistantName = Environment.GetEnvironmentVariable("ASSISTANT_NAME");
    private static readonly string owmKey = Environment.GetEnvironmentVariable("OWM_KEY");
    
    private static OpenAIApi openAIApi = new OpenAIApi();
    private static SpeechConfig? speechConfig;
    private static SpeechSynthesizer? speechSynthesizer;
    private static AudioConfig? audioConfig;
    private static DictationMessageProvider? dictationMessageProvider;
    private static MessageManager? messageManager;
    private static ChoreManager? choreManager;
    private static SpeechManager? speechManager;
    private static SpeechRecognizer? speechRecognizer;
    private static OpenWeatherMapClient? openWeatherMapClient;

    public static double Lat = 47.5534058;
    public static double Long = -122.3093843;


    async static Task Main(string[] args)
    {
        openWeatherMapClient = new OpenWeatherMapClient(owmKey);
        choreManager = new ChoreManager();
        choreManager.LoadChores();
        speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        speechSynthesizer = new SpeechSynthesizer(speechConfig);
        dictationMessageProvider = new DictationMessageProvider(speechRecognizer);
        speechManager = new SpeechManager(speechRecognizer, speechSynthesizer, assistantName);
        //await dictationMessageProvider.StartContinuousRecognitionAsync();
        messageManager = new MessageManager(ParseArguments("prompt", args), assistantName);

        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await dictationMessageProvider.StopContinuousRecognitionAsync();
        };

        var tkn = new CancellationTokenSource().Token;
        //await speechManager.Speak("ET", tkn);

        var weatherToolmessage = await openWeatherMapClient.GetWeatherAsync(Lat, Long, tkn);
        messageManager.AddMessage(new Message { Content = $"OpenWeatherMap current weather (report in Fehrenheit):\n\n{weatherToolmessage}", Role = Role.System});

        var listToolMessage = await choreManager.List(tkn);
        messageManager.AddMessage(new Message { Content = listToolMessage, Role = Role.System});
        
        // This chunk will allow the Assistant to have the first message.
        var chatMessagesForOpening = messageManager.GetChatCompletionRequestMessages();
        chatMessagesForOpening.Last().Content += "\nIn your opening message, you hit the most important bits of information but you still don't forget a bit of levity.";
        var openingMessage = await openAIApi.GetChatCompletionAsync(chatMessagesForOpening, tkn);
        messageManager.AddMessage(openingMessage);
        if (string.IsNullOrEmpty(openingMessage.Content) == false)
        {
            await speechManager.Speak(openingMessage.Content, tkn);
        }
        Console.WriteLine("Speak into your microphone.");
        while (true)
        {
            Console.WriteLine($"[Loop] Waiting for user message");
            var userMessage = await dictationMessageProvider.ReadLine("Eric", tkn);//GetNextMessageAsync(tkn);
            messageManager.AddMessage(userMessage);
            Console.WriteLine($"[Loop] Waiting for tool call message");
            var toolCallMessage = await openAIApi.GetToolCallAsync(messageManager.GetChatCompletionRequestMessages(), tkn);
            messageManager.AddMessage(toolCallMessage);
            Console.WriteLine($"[Loop] Waiting for tool messages");
            var toolMessages = await HandleToolCall(toolCallMessage, tkn);
            messageManager.AddMessages(toolMessages);
            var messagesToSpeak = toolMessages.Where(tm => string.IsNullOrEmpty(tm.Content) == false && tm.Role == Role.Assistant);
            foreach (var msg in messagesToSpeak)
            {
                if (string.IsNullOrEmpty(msg.Content) == false)
                {
                    Console.WriteLine($"[Loop] Waiting for Assistant to finish speaking.");
                    bool isSpechToTextRunning = false;
                    await speechManager.Speak(msg.Content, tkn, isSpechToTextRunning);
                }
            }
        }
    }

    private static async Task<IEnumerable<Message>> HandleToolCall(Message message, CancellationToken cancelToken)
    {
        if (message.ToolCalls == null || message.ToolCalls.Count == 0)
        {
            return new List<Message>();
        }

        var messages = new List<Message>();
        foreach (var call in message.ToolCalls)
        {
            var functionName = call.Function.Name;
            var arguments = call.Function.Arguments;
            var argsJObj = JObject.Parse(arguments);
            Console.WriteLine($"[{assistantName}] {functionName}({arguments})");
            switch (functionName)
            {
                case "file_task":
                    var fileToolMessage = await choreManager.File(call, cancelToken);
                    messages.Add(fileToolMessage);
                    break;
                case "complete_task":
                    var completeToolMessage = await choreManager.Complete(call, cancelToken);
                    messages.Add(completeToolMessage);
                    break;
                case "list_tasks":
                    var listToolMessage = await choreManager.List(call, cancelToken);
                    messages.Add(listToolMessage);
                    var chatMessagesForListTasks = messageManager.GetChatCompletionRequestMessages();
                    chatMessagesForListTasks.AddRange(messages);
                    var listAssistantMessage = await openAIApi.GetChatCompletionAsync(chatMessagesForListTasks, cancelToken);
                    messages.Add(listAssistantMessage);
                    break;
                case "speak":
                    var speakToolMessage = await speechManager.SpeakFromToolCall(call, cancelToken);
                    messages.Add(speakToolMessage);
                    break;
                case "get_weather":
                    var weatherToolmessage = await openWeatherMapClient.GetWeatherAsync(call, Lat, Long, cancelToken);
                    messages.Add(weatherToolmessage);
                    var chatMessagesForGetWeather = messageManager.GetChatCompletionRequestMessages();
                    chatMessagesForGetWeather.AddRange(messages);
                    chatMessagesForGetWeather.Last().Content += "\nUse fahrenheit units.\n";
                    var weatherAssistantMessage = await openAIApi.GetChatCompletionAsync(chatMessagesForGetWeather, cancelToken);
                    messages.Add(weatherAssistantMessage);
                    break;
                default:
                    // Handle unknown function
                    var unknownToolMessage = new Message {
                        Content = $"Unknown tool function name {functionName}",
                        ToolCallId = call.Id,
                        Role = Role.Tool
                    };
                    messages.Add(unknownToolMessage);
                    break;
            }
        }
        return messages;
    }

    static string? ParseArguments(string argName, string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == $"--{argName}" || args[i] == $"-{argName}") && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
