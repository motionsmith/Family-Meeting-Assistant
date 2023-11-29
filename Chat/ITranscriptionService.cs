public interface ITranscriptionService
{
    Task StartTranscriptionAsync(bool addSystemMessage);
    Task StopTranscriptionAsync(bool addSystemMessage);
    bool IsTranscribing {get;}
}