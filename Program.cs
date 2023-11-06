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

    private static Queue<MeaningfulChunk> chunksQueue = new Queue<MeaningfulChunk>();
    private static List<MeaningfulChunk> chunksDequeued = new List<MeaningfulChunk>();
    private static CancellationTokenSource cancellationTokenSrc = new CancellationTokenSource();
    private static Task? meaningfulChunkDequeueLoop;
    private static OpenAIApi openAIApi = new OpenAIApi();
    private static SpeechRecognizer? speechRecognizer;
    private static SpeechConfig? speechConfig;
    private static SpeechSynthesizer? speechSynthesizer;
    private static AudioConfig? audioConfig;

    async static Task Main(string[] args)
    {
        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";
        speechConfig.SpeechSynthesisVoiceName = "en-US-SaraNeural";

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
            chunk.OpenAITask = openAIApi.SendRequestAsync(chunksDequeued);

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
}
