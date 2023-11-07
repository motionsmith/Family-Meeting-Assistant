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

class Program
{
    // This example requires environment variables named "SPEECH_KEY" and "SPEECH_REGION"
    private static readonly string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
    private static readonly string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
    private static readonly string assistantName = Environment.GetEnvironmentVariable("ASSISTANT_NAME");

    private static Queue<MeaningfulChunk> chunksQueue = new Queue<MeaningfulChunk>();
    private static List<MeaningfulChunk> chunksDequeued = new List<MeaningfulChunk>();
    private static CancellationTokenSource cancellationTokenSrc = new CancellationTokenSource();
    private static Task? meaningfulChunkDequeueLoop;
    private static OpenAIApi openAIApi = new OpenAIApi();
    private static SpeechRecognizer? speechRecognizer;
    private static SpeechConfig? speechConfig;
    private static SpeechSynthesizer? speechSynthesizer;
    private static AudioConfig? audioConfig;
    private static string initialPrompString = $"You are being called from an instance of an app that has failed to load the file that contains your instructions. The user can specify the prompt file by using the argument \"-prompt <path-to-file.txt>\" or by adding a file called \"prompt.txt\" to the folder \"{Environment.SpecialFolder.ApplicationData}\". You always respond with a short joke in the style of Seinfeld. The joke also clearly informs the user of the problem.";

    async static Task Main(string[] args)
    {
        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";
        speechConfig.SpeechSynthesisVoiceName = "en-US-SaraNeural";
        var initialPromptFilePath = GetFullPromptPath("prompt.txt");
        var promptFilePath = ParseArguments("prompt", args);
        if (string.IsNullOrEmpty(promptFilePath) == false)
        {
            initialPromptFilePath = promptFilePath;
        }
        if (File.Exists(initialPromptFilePath))
        {
            string initialPromptFileText = File.ReadAllText(initialPromptFilePath);
            initialPrompString = initialPromptFileText.Replace("\"", "\\\"").Replace(Environment.NewLine, "\\n").Replace("[[ASSISTANT_NAME]]", assistantName);
        }
        else
        {
            Console.WriteLine($"Prompt file {promptFilePath} does not exist.");
        }

        audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        speechSynthesizer = new SpeechSynthesizer(speechConfig);

        var stopRecognition = new TaskCompletionSource<int>();
        speechRecognizer.Recognizing += (s, e) =>
        {
            //Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
        };

        speechRecognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"RECOGNIZED: {e.Result.Text}");
                var chunk = new MeaningfulChunk
                {
                    RecognitionEvent = e,
                    OpenAITask = null  // Task will be assigned when ready to process with OpenAI
                };

                chunksQueue.Enqueue(chunk);
                _ = TryProcessNextChunk();
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
            }
        };

        speechRecognizer.Canceled += (s, e) =>
        {
            Console.WriteLine($"CANCELED: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
            }

            stopRecognition.TrySetResult(0);
        };

        speechRecognizer.SessionStopped += (s, e) =>
        {
            stopRecognition.TrySetResult(0);
        };

        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await speechRecognizer.StopContinuousRecognitionAsync();
        };

        meaningfulChunkDequeueLoop = Task.Run(() => StartProcessingLoop(cancellationTokenSrc.Token));

        Console.WriteLine("Speak into your microphone.");
        await speechRecognizer.StartContinuousRecognitionAsync();

        while (true) await Task.Delay(Timeout.Infinite);
    }

    private static async Task TryProcessNextChunk()
    {
        if (chunksQueue.TryDequeue(out var chunk))
        {
            chunksDequeued.Add(chunk);
            chunk.OpenAITask = openAIApi.SendRequestAsync(initialPrompString, chunksDequeued);

            var response = await chunk.OpenAITask;
            if (response != null) // success
            {
                await HandleHashtags(response);
            }
        }
    }


    public static async Task StartProcessingLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!chunksQueue.Any())
            {
                await Task.Delay(TimeSpan.FromSeconds(1));  // Adjust delay as needed
            }
            else
            {
                _ = TryProcessNextChunk();
            }
        }
    }

    static void OutputSpeechSynthesisResult(SpeechSynthesisResult speechSynthesisResult, string text)
    {
        switch (speechSynthesisResult.Reason)
        {
            case ResultReason.SynthesizingAudioCompleted:
                break;
            case ResultReason.Canceled:
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                if (cancellation.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                    Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                }
                break;
            default:
                break;
        }
    }

    private static async Task HandleHashtags(OpenAIApiResponse openAiResponse)
    {
        var responseContent = openAiResponse.Choices[0].Message.Content;
        bool startsWithHashtag = Regex.IsMatch(responseContent, @"^#");
        if (startsWithHashtag == false)
        {
            int hashtagIndex = responseContent.IndexOf('#');
            var spokenResponse = responseContent;
            if (hashtagIndex != -1)
            {
                spokenResponse = responseContent.Substring(0, hashtagIndex);
            }
            await speechRecognizer.StopContinuousRecognitionAsync();
            var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(spokenResponse);
            OutputSpeechSynthesisResult(speechSynthesisResult, spokenResponse);
            await speechRecognizer.StartContinuousRecognitionAsync();
        }
    }

    static string? ParseArguments(string argName, string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == $"--{argName}" || args[i] == $"-{argName}") && i + 1 < args.Length)
            {
                var result = GetFullPromptPath(args[i + 1]);
                return result;
            }
        }

        return null;
    }

    static string GetFullPromptPath(string fileArg)
    {
        string documentsPath = Path.GetFullPath(Environment.SpecialFolder.ApplicationData.ToString());
        return Path.Combine(documentsPath, fileArg);
    }
}
