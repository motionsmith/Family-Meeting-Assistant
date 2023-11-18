using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

class Program
{
    // This example requires environment variables named "SPEECH_KEY" and "SPEECH_REGION"
    private static readonly string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
    private static readonly string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
    private static readonly string assistantName = Environment.GetEnvironmentVariable("ASSISTANT_NAME");

    
    private static CancellationTokenSource cancellationTokenSrc = new CancellationTokenSource();
    
    private static OpenAIApi openAIApi = new OpenAIApi();
    
    private static SpeechConfig? speechConfig;
    private static SpeechSynthesizer? speechSynthesizer;
    private static AudioConfig? audioConfig;
    private static DictationMessageProvider? dictationMessageProvider;
    private static MessageManager? messageManager;
    private static ChoreManager? choreManager;
    private static SpeechManager? speechManager;
    private static SpeechRecognizer? speechRecognizer;
    private static WaitForInstructionsToolManager? waitForInstructionsToolManager;

    async static Task Main(string[] args)
    {
        choreManager = new ChoreManager();
        choreManager.LoadChores();
        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        speechSynthesizer = new SpeechSynthesizer(speechConfig);
        dictationMessageProvider = new DictationMessageProvider(speechRecognizer);
        speechManager = new SpeechManager(speechConfig, speechRecognizer, speechSynthesizer);
        waitForInstructionsToolManager = new WaitForInstructionsToolManager();
        var promptArg = ParseArguments("prompt", args);
        var messageProviders = new IMessageProvider[] { dictationMessageProvider, choreManager, speechManager, waitForInstructionsToolManager};
        messageManager = new MessageManager(messageProviders, promptArg, assistantName);

        messageManager.MessageArrived += async msg => {
            if (msg.Role == Role.User)
            {
                var messages = new List<Message> {
                    messageManager.GenerateMessage()
                };
                messages.AddRange(messageManager.Messages);
                Console.WriteLine($"Kicking off another request!");
                var result = await openAIApi.SendRequestAsync(messages);
                if (result.Error == null)
                {
                    messageManager.Messages.Add(result.Choices[0].Message);
                    await HandleToolCall(result);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(result.Error.Message);
                    Console.ResetColor();
                }
            }
        };

        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await dictationMessageProvider.StopContinuousRecognitionAsync();
        };

        Console.WriteLine("Speak into your microphone.");
        await dictationMessageProvider.StartContinuousRecognitionAsync();

        while (true) await Task.Delay(Timeout.Infinite);
    }

    private static async Task HandleToolCall(OpenAIApiResponse openAiResponse)
    {
        var aiResponseMessage = openAiResponse.Choices[0].Message;
        if (aiResponseMessage.ToolCalls == null)
        {
            return;
        }

        Console.WriteLine($"[{assistantName}] {aiResponseMessage.ToolCalls[0].Function}");
        foreach (var call in aiResponseMessage.ToolCalls)
        {
            var functionName = call.Function.Name;
            var arguments = call.Function.Arguments;
            var argsJObj = JObject.Parse(arguments);

            switch (functionName)
            {
                case "file_task":
                    choreManager.File(call);
                    break;
                case "complete_task":
                    choreManager.Complete(call);
                    break;
                case "list_tasks":
                    choreManager.List(call);
                    break;
                case "speak":
                    await speechManager.Speak(call);
                    break;
                case "wait_for_instructions":
                    waitForInstructionsToolManager.WaitForInstructions(call);
                    break;
                default:
                    // Handle unknown function
                    Console.WriteLine($"Can't call Unknown tool {functionName}");
                    break;
            }
        }
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
