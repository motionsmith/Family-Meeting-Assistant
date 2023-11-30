public interface ITranscriptionService
{
    event Action Recognizing;
    
    Task StartTranscriptionAsync(/*bool addSystemMessage*/);
    Task StopTranscriptionAsync(/*bool addSystemMessage*/);
}