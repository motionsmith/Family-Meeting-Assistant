using System;
using Microsoft.CognitiveServices.Speech;

public class MeaningfulChunk
{
    public SpeechRecognitionEventArgs RecognitionEvent { get; set; }
    public Task<OpenAIApiResponse>? OpenAITask { get; set; }
}

