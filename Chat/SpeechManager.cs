﻿
using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;

public class SpeechManager : IMessageProvider, IChatObserver
{
    public bool IsSynthesizing {get; private set; }
    private ITranscriptionService transcriptionService;
    private SpeechSynthesizer speechSynthesizer;
    private Func<SoundDeviceTypes> soundDeviceGetter;
    private Func<InteractionMode> interactionModeGetter;

    private bool wasInterrupted = false;
    private ConcurrentQueue<Message> newMessageQueue = new ConcurrentQueue<Message>();

    public SpeechManager(ITranscriptionService transcriptionSvc, SpeechSynthesizer speechSynthesizer, SettingsManager settingsManager)
    {
        this.transcriptionService = transcriptionSvc;
        this.speechSynthesizer = speechSynthesizer;
        this.speechSynthesizer.SynthesisStarted += HandleSynthesisStarted;
        this.speechSynthesizer.Synthesizing += HandleSynthesizing;
        this.speechSynthesizer.SynthesisCompleted += HandleSynthesisCompleted;
        this.soundDeviceGetter = settingsManager.GetterFor<ClientSoundDeviceSetting, SoundDeviceTypes>();
        this.interactionModeGetter = settingsManager.GetterFor<InteractionModeSetting, InteractionMode>();
    }

    public Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var newMessages = newMessageQueue.ToArray();
        newMessageQueue.Clear();
        return Task.FromResult(newMessages.AsEnumerable());
    }

    public void InterruptSynthesis()
    {
        wasInterrupted = true;
        Console.WriteLine("Begin stop speaking");
        speechSynthesizer.StopSpeakingAsync().ContinueWith((task) => Console.WriteLine("End stop speaking"));
    }

    public void OnNewMessages(IEnumerable<Message> messages)
    {
        messages = messages.Where(m => 
            m.Role == Role.Assistant && 
            string.IsNullOrEmpty(m.Content) == false &&
            m.Content != "...");
        var speakTask = Task.Run(async () =>
        {
            foreach (var message in messages)
            {
                var speechResult = await SpeakAsync(message);
            }
        });
    }

    public async Task<SpeechSynthesisResult> SpeakAsync(Message message)
    {
        var soundDevice = soundDeviceGetter.Invoke();
        var temporarilyStopRecognition = soundDevice == SoundDeviceTypes.OpenAirSpeakers;
        if (temporarilyStopRecognition)
        {
            await transcriptionService.StopTranscriptionAsync();
        }
        var synthesisResult = await speechSynthesizer.SpeakTextAsync(message.Content);
        CheckIfInterrupted(message);

        // DEBUG 
        if (synthesisResult.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(synthesisResult);
            if (cancellation.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
            }
        }
        
        if (temporarilyStopRecognition)
        {
            await transcriptionService.StartTranscriptionAsync();
        }
        return synthesisResult;
    }

    private void CheckIfInterrupted(Message message)
    {
        var interrupted = wasInterrupted;
        wasInterrupted = false;
        if (interrupted)
        {
            if (message.Content.Length > 10)
            {
                message.Content = message.Content.Substring(0, 10) + "—";
            }
            var scolding = new Message
            {
                Role = Role.System,
                Content = $"The Client interrupted."
            };
            newMessageQueue.Enqueue(scolding);
        }
    }

    private void HandleSynthesisStarted(object? sender, SpeechSynthesisEventArgs e)
    {
        IsSynthesizing = true;
    }

    private void HandleSynthesizing(object? sender, SpeechSynthesisEventArgs e)
    {
        IsSynthesizing = true;
    }

    private void HandleSynthesisCompleted(object? sender, SpeechSynthesisEventArgs e)
    {
        IsSynthesizing = false;
        if (wasInterrupted)
        {
            // Handle interruption case
            Console.WriteLine("[Debug] Interrupted");
        }
    }
}
