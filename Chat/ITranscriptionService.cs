public interface ITranscriptionService
{
    event Action Recognizing;
    event Action Recognized;
    event Action SessionStarted;
    event Action SessionStopped;
    
    Task StartTranscriptionAsync();
    Task StopTranscriptionAsync();
}