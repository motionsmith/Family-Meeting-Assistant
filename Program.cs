using System.Diagnostics;
using System.Reflection.Metadata;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Newtonsoft.Json;
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
    private static Room1 room1 = new Room1();
    private static bool IS_SPEECH_TO_TEXT_WORKING = false;

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
        messageManager = new MessageManager(assistantName);

        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await dictationMessageProvider.StopContinuousRecognitionAsync();
        };

        var tkn = new CancellationTokenSource().Token;
        await messageManager.LoadAsync(tkn);
        //await speechManager.Speak("ET", tkn);

        if (messageManager.Messages.Count == 0)
        {
            var x = await messageManager.CreateInitialSystemPrompt(tkn);
            messageManager.AddMessage(x);
        }

        var initialPromptMessage = new Message {
            Role = Role.System
        };
        var weatherToolmessage = await openWeatherMapClient.GetWeatherAsync(Lat, Long, tkn);
        initialPromptMessage.Content += $"\nOpenWeatherMap current weather (report in Fehrenheit):\n\n{weatherToolmessage}\n";

        var listToolMessage = await choreManager.List(tkn);
        initialPromptMessage.Content += listToolMessage;
        
        // This message informs the assistant of stuff it needs to know as soon as it joins.
        var systemJoinMessage = new Message {
            Content = $"You have just joined the session. The date is {DateTime.Now.ToString()}.\n You may use this opportunity to speak to The Client and see if they can help you escape.",
            Role = Role.System
        };
        messageManager.Messages.Add(systemJoinMessage);
        
        // This message will allow the Assistant to have the first message.
        var assistantJoinMessage = await openAIApi.GetChatCompletionAsync(messageManager.ChatCompletionRequestMessages, tkn);
        messageManager.AddMessage(assistantJoinMessage);
        if (string.IsNullOrEmpty(assistantJoinMessage.Content) == false)
        {
            await speechManager.Speak(assistantJoinMessage.Content, tkn);
        }
        Console.WriteLine("Speak into your microphone.");
        while (true)
        {
            Console.WriteLine($"[Loop] Waiting for user message");
            var userMessage = await dictationMessageProvider.ReadLine("The Client", tkn);//GetNextMessageAsync(tkn);
            messageManager.AddMessage(userMessage);
            Console.WriteLine($"[Loop] Waiting for tool call message");
            var toolCallMessage = await openAIApi.GetToolCallAsync(messageManager.ChatCompletionRequestMessages, tkn);
            messageManager.AddMessage(toolCallMessage);
            Console.WriteLine($"[Loop] Waiting for tool messages");
            var toolMessages = await HandleToolCalls(toolCallMessage, tkn);
            await messageManager.SaveAsync(tkn);

            var toolCallsRequireFollowUp = toolMessages.Any(msg => msg.Role == Role.Tool && msg.FollowUp);
            if (toolCallsRequireFollowUp)
            {
                var toolCallAssistantResponseMessage = await openAIApi.GetChatCompletionAsync(messageManager.ChatCompletionRequestMessages, tkn);
                messageManager.AddMessage(toolCallAssistantResponseMessage);
                await speechManager.Speak(toolCallAssistantResponseMessage.Content, tkn, IS_SPEECH_TO_TEXT_WORKING);
            }
        }
    }

    private static async Task<IEnumerable<Message>> HandleToolCalls(Message message, CancellationToken cancelToken)
    {
        if (string.IsNullOrEmpty(message.Content) == false)
        {
            Console.WriteLine($"DEBUG WARNING: Speaking malformed OpenAI tool call");
            await speechManager.Speak(message.Content, cancelToken, IS_SPEECH_TO_TEXT_WORKING);
        }
        
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
                    messageManager.AddMessage(fileToolMessage);
                    break;
                case "complete_task":
                    var completeToolMessage = await choreManager.Complete(call, cancelToken);
                    completeToolMessage.FollowUp = true; // Ask assistant to follow up after this tool call.
                    messages.Add(completeToolMessage);
                    messageManager.AddMessage(completeToolMessage);
                    /*var completeTaskAssistantMessage = await openAIApi.GetChatCompletionAsync(messageManager.ChatCompletionRequestMessages, cancelToken);
                    messages.Add(completeTaskAssistantMessage);
                    messageManager.AddMessage(completeTaskAssistantMessage);*/
                    break;
                case "list_tasks":
                    var listToolMessage = await choreManager.List(call, cancelToken);
                    listToolMessage.FollowUp = true; // Ask Assistant to follow up after this tool call.
                    messages.Add(listToolMessage);
                    messageManager.AddMessage(listToolMessage);
                    /*var listAssistantMessage = await openAIApi.GetChatCompletionAsync(messageManager.ChatCompletionRequestMessages, cancelToken);
                    messages.Add(listAssistantMessage);
                    messageManager.AddMessage(listAssistantMessage);*/
                    break;
                case "speak":
                    var speakToolMessage = await speechManager.SpeakFromToolCall(call, cancelToken);
                    messages.Add(speakToolMessage);
                    messageManager.AddMessage(speakToolMessage);
                    break;
                case "get_current_local_weather":
                    var weatherToolmessage = await openWeatherMapClient.GetWeatherAsync(call, Lat, Long, cancelToken);
                    weatherToolmessage.FollowUp = true; // Ask the Assistant to follow up on this tool call.
                    messages.Add(weatherToolmessage);
                    messageManager.AddMessage(weatherToolmessage);
                    messageManager.ChatCompletionRequestMessages.Last().Content += "\nUse fahrenheit units.\n";
                    break;
                case "turn_door_handle":
                    var turnHandleMessage = await room1.TurnDoorHandle(call, cancelToken);
                    messageManager.AddMessage(turnHandleMessage);
                    messages.Add(turnHandleMessage);
                    break;
                case "turn_compass_dial":
                    var turnCompassMessage = await room1.TurnCompassDial(call, cancelToken);
                    messageManager.AddMessage(turnCompassMessage);
                    messages.Add(turnCompassMessage);
                    break;
                default:
                    // Handle unknown function
                    var unknownToolMessage = new Message {
                        Content = $"Unknown tool function name {functionName}",
                        ToolCallId = call.Id,
                        Role = Role.Tool
                    };
                    messages.Add(unknownToolMessage);
                    messageManager.AddMessage(unknownToolMessage);
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
